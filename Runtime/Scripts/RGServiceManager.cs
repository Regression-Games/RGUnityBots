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
        
        private const string GAME_ENGINE_HOST = "localhost";
        private const int  GAME_ENGINE_PORT = 19999;

        private const string RGSERVICE_HOST = "http://localhost";
        private const int RGSERVICE_PORT = 8080;
        
        protected static RGServiceManager _this = null;

        protected virtual void Awake()
        {
            // only allow 1 of these to be alive
            if( _this != null && this.gameObject != _this.gameObject)
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
                RGSettings rgSettings = RGSettings.GetOrCreateSettings();
                string email=rgSettings.GetEmail();
                string password = rgSettings.GetPassword();

                if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
                {
                    await Auth(
                        email: email,
                        password: password, 
                        onSuccess: s =>
                        {
                            rgAuthToken = s;
                            Debug.Log($"Signed in to RG Service; token: {rgAuthToken}");
                        },
                        onFailure: f =>
                        {
                            Debug.Log($"Failed signing in to RG Service");
                        }
                    );
                    return rgAuthToken != null;
                }
                Debug.LogWarning("RG Service email or password not configured");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
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

        public async Task Auth(string email, string password, Action<string> onSuccess, Action<string> onFailure)
        {
            Debug.Log($"Calling RGService Auth for email: {email}, password: *********");
            await SendWebRequest(
                uri: $"{RGSERVICE_HOST}:{RGSERVICE_PORT}/auth",
                method: "POST",
                payload: JsonUtility.ToJson(new RGAuthRequest(email, password)),
                onSuccess: async(s) =>
                {
                    RGAuthResponse response = JsonUtility.FromJson<RGAuthResponse>(s);
                    Debug.Log($"RGService Auth response received with token: {response.token}");
                    rgAuthToken = response.token;
                    onSuccess.Invoke(response.token);
                },
                onFailure: async(f) =>
                {
                    Debug.LogWarning(f);
                    onFailure.Invoke(f);
                }
            );
        }
        
        public async Task GetBotsForCurrentUser(Action<RGBot[]> onSuccess, Action onFailure)
        {
            await EnsureAuthed();
            await SendWebRequest(
                uri: $"{RGSERVICE_HOST}:{RGSERVICE_PORT}/bot",
                method: "GET",
                payload: null,
                onSuccess: async (s) =>
                {
                    // wrapper this as C#/Unity json can't handle top level arrays /yuck
                    string theNewText = $"{{\"bots\":{s}}}";
                    RGBotList response = JsonUtility.FromJson<RGBotList>(theNewText);
                    Debug.Log($"RGService GetBotsForCurrentUser response received with bots: {string.Join(",", response.bots.ToList())}");
                    onSuccess.Invoke(response.bots);
                },
                onFailure: async (f) =>
                {
                    onFailure.Invoke();
                }
            );
        }

        public async Task GetExternalConnectionInformationForBotInstance(long botInstanceId, Action<RGBotInstanceExternalConnectionInfo> onSuccess, Action onFailure)
        {
            await EnsureAuthed();
            try
            {
                await SendWebRequest(
                    uri: $"{RGSERVICE_HOST}:{RGSERVICE_PORT}/matchmaking/running-bot/{botInstanceId}/external-connection-info",
                    method: "GET",
                    payload: null,
                    onSuccess: async (s) =>
                    {
                        RGBotInstanceExternalConnectionInfo connInfo =
                            JsonUtility.FromJson<RGBotInstanceExternalConnectionInfo>(s);
                        Debug.Log($"RG Bot Instance external connection info: {connInfo}");
                        onSuccess.Invoke(connInfo);
                    },
                    onFailure: async (f) => { onFailure.Invoke(); }
                );
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public async Task QueueInstantBot(long botId, Action<RGBotInstance> onSuccess, Action onFailure)
        {
            await EnsureAuthed();
            await SendWebRequest(
                uri: $"{RGSERVICE_HOST}:{RGSERVICE_PORT}/matchmaking/instant-bot/queue",
                method: "POST",
                payload: JsonUtility.ToJson(new RGQueueInstantBotRequest(GAME_ENGINE_HOST, GAME_ENGINE_PORT, botId, RG_UNITY_AUTH_TOKEN)),
                onSuccess: async (s) =>
                {
                    RGBotInstance botInstance = JsonUtility.FromJson<RGBotInstance>(s);
                    Debug.Log($"RG Bot Instance id: {botInstance.id} started");
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
                uri: $"{RGSERVICE_HOST}:{RGSERVICE_PORT}/matchmaking/running-bot/{botId}",
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
                uri: $"{RGSERVICE_HOST}:{RGSERVICE_PORT}/matchmaking/running-bot/{botInstanceId}/stop",
                method: "POST",
                payload: null,
                onSuccess: async (s) =>
                {
                    onSuccess.Invoke();
                },
                onFailure: async (f) =>
                {
                    onFailure.Invoke();
                }
            );
        }

        /**
         * MUST be called on main thread only... This is because `new UnityWebRequest` makes a .Create call internally
         */
        private async Task<bool> SendWebRequest(string uri, string method, string payload, Func<string, Task> onSuccess, Func<string, Task> onFailure)
        {
            Debug.Log($"Calling {uri} - {method} - payload: {payload}");
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
                    Debug.Log($"Response from {uri} - {method}\r\n{resultText}");
                    await onSuccess.Invoke(resultText);
                    return true;
                }

                string errorString =
                    $"Error calling {uri} - {method} - {request.error} - {request.result} - {request.downloadHandler?.text}";
                Debug.LogWarning(errorString);
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
        }


    }
}
