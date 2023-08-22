using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RegressionGames.Types;
using UnityEngine;
using UnityEngine.Networking;

namespace RegressionGames
{
    public class RGServiceManager : MonoBehaviour
    { // not really a monobehaviour.. but we make one of these on an object

        public static readonly string RG_UNITY_AUTH_TOKEN = Guid.NewGuid().ToString();

        private string rgAuthToken;

        protected static RGServiceManager _this = null;

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
                var apiKey = RGEnvVars.ReadAPIKey();
                if (apiKey != null && apiKey.Trim() != "")
                {
                    RGDebug.Log("Using API Key from env var rather than username/password for auth");
                    rgAuthToken = apiKey.Trim();
                    return true;
                }
                
                RGDebug.Log("Using username/password rather than env var for auth");
                RGSettings rgSettings = RGSettings.GetOrCreateSettings();
                string email = rgSettings.GetEmail();
                string password = rgSettings.GetPassword();

                if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
                {
                    await Auth(
                        email: email,
                        password: password,
                        onSuccess: s =>
                        {
                            rgAuthToken = s;
                        },
                        onFailure: f =>
                        {
                            
                        }
                    );
                    return rgAuthToken != null;
                }
                RGDebug.LogWarning("RG Service email or password not configured");
            }
            catch (Exception ex)
            {
                RGDebug.LogException(ex);
            }

            return false;
        }

        private async Task<bool> EnsureAuthed()
        {
            if (!IsAuthed())
            {
                return await TryAuth();
            }

            return true;
        }


        private String GetRgServiceBaseUri()
        {
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            string host = rgSettings.GetRgHostAddress();
            
            // If env var is set, use that instead
            string hostOverride = RGEnvVars.ReadHost();
            if (hostOverride != null && hostOverride.Trim() != "")
            {
                RGDebug.Log("Using host from environment variable rather than RGSettings");
                host = hostOverride.Trim();
            }
            else
            {
                RGDebug.Log("Using host from RGSettings rather than env var");
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
                    RGDebug.LogVerbose($"RGService Auth response received with token: {response.token}");
                    rgAuthToken = response.token;
                    onSuccess.Invoke(response.token);
                },
                onFailure: async (f) =>
                {
                    RGDebug.LogWarning($"Failed signing in to RG Service - ${f}");
                    onFailure.Invoke(f);
                }
            );
        }

        public async Task GetBotsForCurrentUser(Action<RGBot[]> onSuccess, Action onFailure)
        {
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            string host = rgSettings.GetRgHostAddress();
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
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            await EnsureAuthed();
            try
            {
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
            catch (Exception ex)
            {
                RGDebug.LogException(ex);
            }
        }

        public async Task QueueInstantBot(long botId, Action<RGBotInstance> onSuccess, Action onFailure)
        {
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            string host = rgSettings.GetRgHostAddress();
            await EnsureAuthed();
            await SendWebRequest(
                uri: $"{GetRgServiceBaseUri()}/matchmaking/instant-bot/queue",
                method: "POST",
                payload: JsonUtility.ToJson(new RGQueueInstantBotRequest("unused", 0, botId, RG_UNITY_AUTH_TOKEN)), // TODO Remove host and port from payload if they're optional
                onSuccess: async (s) =>
                {
                    RGBotInstance botInstance = JsonUtility.FromJson<RGBotInstance>(s);
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
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
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
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            string host = rgSettings.GetRgHostAddress();
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
        private async Task<bool> SendWebRequest(string uri, string method, string payload, Func<string, Task> onSuccess, Func<string, Task> onFailure)
        {
            RGDebug.LogVerbose($"Calling {uri} - {method} - payload: {payload}");
            UnityWebRequest request = new UnityWebRequest(uri, method);
            SetupWebRequest(request, (payload == null ? null : Encoding.UTF8.GetBytes(payload)));
            UnityWebRequestAsyncOperation asyncOperation = request.SendWebRequest();
            try
            {
                while (!asyncOperation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string resultText = request.downloadHandler?.text;
                    // pretty print
                    RGDebug.LogVerbose($"Response from {uri} - {method}\r\n{resultText}");
                    await onSuccess.Invoke(resultText);
                    return true;
                }

                // since we call this frequently waiting for bots to come online, we log this at a more debug level
                string errorString =
                    $"Error calling {uri} - {method} - {request.error} - {request.result} - {request.downloadHandler?.text}";
                RGDebug.LogDebug(errorString);
                await onFailure.Invoke(errorString);
                return false;
            }
            catch (Exception ex)
            {
                await onFailure.Invoke(ex.ToString());
                return false;
            }
            finally
            {
                asyncOperation.webRequest?.Dispose();
            }
        }


        private void SetupWebRequest(UnityWebRequest webRequest, byte[] payload)
        {
            UploadHandler uh = new UploadHandlerRaw(payload);
            uh.contentType = "application/json";
            webRequest.uploadHandler = uh;
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "application/json");
            if (rgAuthToken != null)
            {
                webRequest.SetRequestHeader("Authorization", $"Bearer {rgAuthToken}");
            }

            if (webRequest.uri.Scheme.Equals(Uri.UriSchemeHttps))
            {
                webRequest.certificateHandler = RGCertOnlyPublicKey.GetInstance();
            }
        }


    }
}
