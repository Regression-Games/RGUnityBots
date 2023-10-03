using System;
using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RegressionGames.Types;
using UnityEngine;
using UnityEngine.Networking;

namespace RegressionGames
{
    public class RGServiceManager : MonoBehaviour
    { // not really a monobehaviour.. but we make one of these on an object

        public static readonly string RG_UNITY_AUTH_TOKEN = Guid.NewGuid().ToString();

        // 30 seconds
        public static readonly int WEB_REQUEST_TIMEOUT_SECONDS = 30;

        private static Mutex authLock = new Mutex(false,"AuthLock");

        private string rgAuthToken;

        protected static RGServiceManager _this = null;

        private long correlationId = 0L;

        protected virtual void Awake()
        {
            // only allow 1 of these to be alive
            if (_this != null && this.gameObject != _this.gameObject)
            {
                Destroy(this.gameObject);
                return;
            }
            // keep this thing alive across scenes
            DontDestroyOnLoad(this.gameObject);
            _this = this;
        }

        public static RGServiceManager GetInstance()
        {
            return _this;
        }

        public bool IsAuthed()
        {
            return rgAuthToken != null;
        }

        public async Task<bool> TryAuth()
        {
            try
            {
                // If an API key was given, just return and set that
                var apiKey = RGEnvConfigs.ReadAPIKey();
                if (apiKey != null && apiKey.Trim() != "")
                {
                    RGDebug.LogDebug("Using API Key from env var rather than username/password for auth");
                    rgAuthToken = apiKey.Trim();
                }
                else
                {
                    RGDebug.LogDebug("Using username/password rather than env var for auth");
                    RGSettings rgSettings = RGSettings.GetOrCreateSettings();
                    string email = rgSettings.GetEmail();
                    string password = rgSettings.GetPassword();

                    if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
                    {
                        await Auth(
                            email: email,
                            password: password,
                            onSuccess: s => { rgAuthToken = s; },
                            onFailure: f => { }
                        );
                    }
                    else
                    {
                        RGDebug.LogWarning("RG Service email or password not configured");
                    }
                }
            }
            catch (Exception ex)
            {
                RGDebug.LogException(ex);
            }

            return IsAuthed();
        }

        private async Task<bool> EnsureAuthed()
        {
            // double lock checking paradigm to avoid locking when possible
            // we lock this so that multiple APIs called close in time at startup
            // don't all first off an auth request in parallel
            if (!IsAuthed())
            {
                authLock.WaitOne();
                try
                {
                    if (!IsAuthed())
                    {
                        return await TryAuth();
                    }
                }
                finally
                {
                    authLock.ReleaseMutex();
                }

            }
            return true;
        }


        private String GetRgServiceBaseUri()
        {
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            string host = rgSettings.GetRgHostAddress();
            
            // If env var is set, use that instead
            string hostOverride = RGEnvConfigs.ReadHost();
            if (hostOverride != null && hostOverride.Trim() != "")
            {
                host = hostOverride.Trim();
            }

            if (host.EndsWith('/'))
            {
                host = host.Substring(0, host.Length - 1);
            }
            
            Uri hostUri = new(host);
            if (hostUri.IsLoopback)
            {
                return $"{host}";
            }
            else
            {
                return $"{host}/rgservice";
            }
        }

        public async Task Auth(string email, string password, Action<string> onSuccess, Action<string> onFailure)
        {
            RGDebug.LogInfo($"Signing in to RGService at {GetRgServiceBaseUri()}");
            await SendWebRequest(
                uri: $"{GetRgServiceBaseUri()}/auth",
                method: "POST",
                payload: JsonUtility.ToJson(new RGAuthRequest(email, password)),
                onSuccess: async (s) =>
                {
                    RGAuthResponse response = JsonUtility.FromJson<RGAuthResponse>(s);
                    RGDebug.LogInfo($"Signed in to RG Service");
                    rgAuthToken = response.token;
                    onSuccess.Invoke(response.token);
                },
                onFailure: async (f) =>
                {
                    RGDebug.LogWarning($"Failed signing in to RG Service - {f}");
                    onFailure.Invoke(f);
                },
                isAuth: true
            );
        }

        public async Task GetBotsForCurrentUser(Action<Types.RGBot[]> onSuccess, Action onFailure)
        {
            await EnsureAuthed();
            await SendWebRequest(
                uri: $"{GetRgServiceBaseUri()}/bot",
                method: "GET",
                payload: null,
                onSuccess: async (s) =>
                {
                    // wrapper this as C#/Unity json can't handle top level arrays /yuck
                    string theNewText = $"{{\"bots\":{s}}}";
                    RGBotList response = JsonUtility.FromJson<RGBotList>(theNewText);
                    RGDebug.LogDebug($"RGService GetBotsForCurrentUser response received with bots: {string.Join(",", response.bots.ToList())}");
                    onSuccess.Invoke(response.bots);
                },
                onFailure: async (f) =>
                {
                    RGDebug.LogWarning($"Failed retrieving bots for current user: {f}");
                    onFailure.Invoke();
                }
            );
        }

        public async Task GetExternalConnectionInformationForBotInstance(long botInstanceId, Action<RGBotInstanceExternalConnectionInfo> onSuccess, Action onFailure)
        {
            await EnsureAuthed();
            await SendWebRequest(
                uri: $"{GetRgServiceBaseUri()}/matchmaking/running-bot/{botInstanceId}/external-connection-info",
                method: "GET",
                payload: null,
                onSuccess: async (s) =>
                {
                    RGBotInstanceExternalConnectionInfo connInfo =
                        JsonUtility.FromJson<RGBotInstanceExternalConnectionInfo>(s);
                    RGDebug.LogDebug($"RG Bot Instance external connection info: {connInfo}");
                    onSuccess.Invoke(connInfo);
                },
                onFailure: async (f) =>
                {
                    onFailure.Invoke();
                }
            );
        }

        public async Task QueueInstantBot(long botId, Action<RGBotInstance> onSuccess, Action onFailure)
        {
            await EnsureAuthed();
            await SendWebRequest(
                uri: $"{GetRgServiceBaseUri()}/matchmaking/instant-bot/queue",
                method: "POST",
                payload: JsonUtility.ToJson(new RGQueueInstantBotRequest("unused", 0, botId, RG_UNITY_AUTH_TOKEN)), // TODO Remove host and port from payload if they're optional
                onSuccess: async (s) =>
                {
                    RGBotInstance botInstance = JsonUtility.FromJson<RGBotInstance>(s);
                    RGBotServerListener.GetInstance().SetUnityBotState((uint) botInstance.id, RGUnityBotState.STARTING);
                    RGDebug.LogInfo($"Bot Instance id: {botInstance.id} started");
                    onSuccess.Invoke(botInstance);
                },
                onFailure: async (f) =>
                {
                    onFailure.Invoke();
                }
            );
        }

        public async Task GetRunningInstancesForBot(long botId, Action<RGBotInstance[]> onSuccess, Action onFailure)
        {
            await EnsureAuthed();
            await SendWebRequest(
                uri: $"{GetRgServiceBaseUri()}/matchmaking/running-bot/{botId}",
                method: "GET",
                payload: null,
                onSuccess: async (s) =>
                {
                    // wrapper this as C#/Unity json can't handle top level arrays /yuck
                    string theNewText = $"{{\"botInstances\":{s}}}";
                    RGBotInstanceList botInstanceList = JsonUtility.FromJson<RGBotInstanceList>(theNewText);
                    onSuccess.Invoke(botInstanceList.botInstances);
                },
                onFailure: async (f) =>
                {
                    onFailure.Invoke();
                }
            );
        }

        public async Task StopBotInstance(long botInstanceId, Action onSuccess, Action onFailure)
        {
            await EnsureAuthed();
            await SendWebRequest(
                uri: $"{GetRgServiceBaseUri()}/matchmaking/running-bot/{botInstanceId}/stop",
                method: "POST",
                payload: null,
                onSuccess: async (s) =>
                {
                    onSuccess.Invoke();
                },
                onFailure: async (f) =>
                {
                    RGDebug.LogWarning($"Failed to stop bot instance {botInstanceId}: {f}");
                    onFailure.Invoke();
                }
            );
        }

        /**
         * MUST be called on main thread only... This is because `new UnityWebRequest` makes a .Create call internally
         */
        private async Task SendWebRequest(string uri, string method, string payload, Func<string, Task> onSuccess, Func<string, Task> onFailure, bool isAuth=false)
        {
            var messageId = ++correlationId;
            // don't log the details of auth requests :)
            string payloadToLog = isAuth ? "{***:***, ...}" : payload;
            RGDebug.LogVerbose($"<{messageId}> API request - {method}  {uri}{(!string.IsNullOrEmpty(payload)?$"\r\n{payloadToLog}":"")}");
            UnityWebRequest request = new UnityWebRequest(uri, method);

            try
            {
            	SetupWebRequest(request, (payload == null ? null : Encoding.UTF8.GetBytes(payload)), isAuth);
                var task = request.SendWebRequest();
                RGDebug.LogVerbose($"<{messageId}> API request sent ...");
                await new UnityWebRequestAwaiter(task);
                RGDebug.LogVerbose($"<{messageId}> API request complete ...");
                
                string resultText = request.downloadHandler?.text;
                string resultToLog = isAuth ? "{***:***, ...}" : resultText;
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    // pretty print
                    RGDebug.LogVerbose($"<{messageId}> API response - {method}  {uri}\r\n{resultToLog}");
                    await onSuccess.Invoke(resultText);
                }
                else
                {
                    // since we call this method frequently waiting for bots to come online, we log this at a more debug level
                    string errorString =
                        $"<{messageId}> API error - {method}  {uri}\r\n{request.error} - {request.result} - {resultToLog}";
                    RGDebug.LogDebug(errorString);
                    await onFailure.Invoke(errorString);
                }
            }
            catch (Exception ex)
            {
                // since we call this method frequently waiting for bots to come online, we log this at a more debug level
                string errorString =
                    $"<{messageId}> API Exception - {method}  {uri} - {ex}";
                RGDebug.LogDebug(errorString);
                await onFailure.Invoke(errorString);
            }
            finally
            {
                request?.Dispose();
            }

        }


        private void SetupWebRequest(UnityWebRequest webRequest, byte[] payload, bool isAuth=false)
        {
            webRequest.timeout = WEB_REQUEST_TIMEOUT_SECONDS;
            UploadHandler uh = new UploadHandlerRaw(payload);
            uh.contentType = "application/json";
            webRequest.uploadHandler = uh;
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "application/json");
            if (rgAuthToken != null && !isAuth)
            {
                webRequest.SetRequestHeader("Authorization", $"Bearer {rgAuthToken}");
            }

            if (webRequest.uri.Scheme.Equals(Uri.UriSchemeHttps))
            {
                webRequest.certificateHandler = RGCertOnlyPublicKey.GetInstance();
            }
        }
    }

    public readonly struct UnityWebRequestAwaiter : INotifyCompletion
    {
        private readonly UnityWebRequestAsyncOperation _asyncOperation;
        
        
        public UnityWebRequestAwaiter( UnityWebRequestAsyncOperation asyncOperation ) => _asyncOperation = asyncOperation;

        public UnityWebRequestAwaiter GetAwaiter()
        {
            return this;
        }
        
        public UnityWebRequest GetResult() => _asyncOperation.webRequest;
        
        public void OnCompleted( Action continuation ) => _asyncOperation.completed += _ => continuation();
        
        public bool IsCompleted => _asyncOperation.isDone;
    }
}
