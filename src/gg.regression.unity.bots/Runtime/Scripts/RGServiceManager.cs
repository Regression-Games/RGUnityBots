using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RegressionGames.StateActionTypes;
using RegressionGames.Types;
using UnityEngine;
using UnityEngine.Networking;

namespace RegressionGames
{
    [HelpURL("https://docs.regression.gg/studios/unity/unity-sdk/overview")]
    public class RGServiceManager : MonoBehaviour
    { // not really a monobehaviour.. but we make one of these on an object

        public static readonly string RG_UNITY_AUTH_TOKEN = Guid.NewGuid().ToString();

        // 10 seconds
        public static readonly int WEB_REQUEST_TIMEOUT_SECONDS = 10;

        // 1 hour
        public static readonly int WEB_REQUEST_FILE_TIMEOUT_SECONDS = 60 * 60;

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
                    RGUserSettings rgSettings = RGUserSettings.GetOrCreateUserSettings();
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
                while (!authLock.WaitOne(1_000));

                try
                {
                    if (!IsAuthed())
                    {
                        var result = await TryAuth();
                        if (!result)
                        {
                            RGDebug.LogWarning(
                                $"Failed to authenticate with the Regression Games server at {GetRgServiceBaseUri()}");
                        }

                        return result;
                    }
                    else
                    {
                        return true;
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
                onSuccess: (s) =>
                {
                    RGAuthResponse response = JsonUtility.FromJson<RGAuthResponse>(s);
                    RGDebug.LogInfo($"Signed in to RG Service");
                    rgAuthToken = response.token;
                    onSuccess.Invoke(response.token);
                },
                onFailure: (f) =>
                {
                    RGDebug.LogWarning($"Failed signing in to RG Service - {f}");
                    onFailure.Invoke(f);
                },
                isAuth: true
            );
        }

        public async Task GetBotsForCurrentUser(Action<RGBot[]> onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebRequest(
                    uri: $"{GetRgServiceBaseUri()}/bot",
                    method: "GET",
                    payload: null,
                    onSuccess: (s) =>
                    {
                        // wrapper this as C#/Unity json can't handle top level arrays /yuck
                        string theNewText = $"{{\"bots\":{s}}}";
                        var response = JsonConvert.DeserializeObject<RGBotList>(theNewText);
                        RGDebug.LogDebug(
                            $"RGService GetBotsForCurrentUser response received with bots: {string.Join(",", response.bots.ToList())}");
                        onSuccess.Invoke(response.bots);
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed retrieving bots for current user: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure();
            }
        }

        public async Task CreateBot(RGCreateBotRequest request, Action<RGBot> onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebRequest(
                    uri: $"{GetRgServiceBaseUri()}/bot",
                    method: "POST",
                    payload: JsonUtility.ToJson(request),
                    onSuccess: (s) =>
                    {
                        var response = JsonConvert.DeserializeObject<RGBot>(s);
                        RGDebug.LogDebug(
                            $"RGService CreateBot response received: {response}");
                        onSuccess.Invoke(response);
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to create bot for current user: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure();
            }
        }

        public async Task GetBotCodeDetails(long botId, Action<RGBotCodeDetails> onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebRequest(
                    uri: $"{GetRgServiceBaseUri()}/bot/{botId}/code-details",
                    method: "GET",
                    payload: null,
                    onSuccess: (s) =>
                    {
                        RGBotCodeDetails response = JsonConvert.DeserializeObject<RGBotCodeDetails>(s);
                        RGDebug.LogDebug(
                            $"RGService GetBotCodeDetails response received: {response}");
                        onSuccess.Invoke(response);
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to get bot code details for bot id: {botId} - {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure();
            }
        }

        public async Task UpdateBotCode(long botId, string filePath, Action<RGBotCodeDetails> onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebFileUploadRequest(
                    uri: $"{GetRgServiceBaseUri()}/bot/{botId}/update-code",
                    method: "POST",
                    filePath: filePath,
                    contentType: "application/zip",
                    onSuccess: (s) =>
                    {
                        // wrapper this as C#/Unity json can't handle top level arrays /yuck
                        RGBotCodeDetails response = JsonUtility.FromJson<RGBotCodeDetails>(s);
                        RGDebug.LogDebug(
                            $"RGService GetBotCodeDetails response received: {response}");
                        onSuccess.Invoke(response);
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to get bot code details for bot id: {botId} - {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure();
            }
        }

        public async Task DownloadBotCode(long botId, string destinationFilePath, Action onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebFileDownloadRequest(
                    uri: $"{GetRgServiceBaseUri()}/bot/{botId}/download-code",
                    method: "GET",
                    payload: null,
                    destinationFilePath: destinationFilePath,
                    onSuccess: () =>
                    {
                        RGDebug.LogDebug(
                            $"RGService DownloadBotCode response received for bot id: {botId}");
                        onSuccess.Invoke();
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to DownloadBotCode for bot id: {botId} - {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure();
            }
        }

        public async Task UploadScreenshot(long botInstanceId, long tick, string filePath, Action onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebFileUploadRequest(
                    uri: $"{GetRgServiceBaseUri()}/bot-instance-history/{botInstanceId}/screenshots/{tick}",
                    method: "POST",
                    filePath: filePath,
                    contentType: "image/jpeg",
                    onSuccess: (_) => { onSuccess.Invoke(); },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to upload screenshot for bot instance: {botInstanceId} - {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure();
            }

        }

        /**
         * Create a new record in the Bot Instance table, which then allows us to upload results related
         * to a bot run through a Bot Instance History later on.
         */
        public async Task CreateBotInstance(long botId, DateTime startDate, Action<RGBotInstance> onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebRequest(
                    uri: $"{GetRgServiceBaseUri()}/bot/{botId}/bot-instance",
                    method: "POST",
                    payload: JsonConvert.SerializeObject(new RGCreateBotInstanceRequest(startDate)),
                    onSuccess: (s) =>
                    {
                        RGBotInstance response = JsonUtility.FromJson<RGBotInstance>(s);
                        RGDebug.LogDebug(
                            $"RGService CreateBotInstance response received: {response}");
                        onSuccess.Invoke(response);
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to create bot instance record: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure.Invoke();
            }
        }

        /**
         * Create a new record in the Bot Instance History table, which then allows us to upload results related
         * to a bot run.
         */
        public async Task CreateBotInstanceHistory(long botInstanceId, Action<RGBotInstanceHistory> onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebRequest(
                    uri: $"{GetRgServiceBaseUri()}/bot-instance-history/{botInstanceId}",
                    method: "POST",
                    payload: null,
                    onSuccess: (s) =>
                    {
                        RGBotInstanceHistory response = JsonUtility.FromJson<RGBotInstanceHistory>(s);
                        RGDebug.LogDebug(
                            $"RGService CreateBotInstanceHistory response received: {response}");
                        onSuccess.Invoke(response);
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to create bot instance history record: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure.Invoke();
            }
        }

        /**
         * Uploads the zip with bot replay data for a given bot instance
         */
        public async Task UploadReplayData(long botInstanceId, string filePath, Action onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebFileUploadRequest(
                    uri: $"{GetRgServiceBaseUri()}/bot-instance-history/{botInstanceId}/replay-data",
                    method: "POST",
                    filePath: filePath,
                    contentType: "application/zip",
                    onSuccess: (s) =>
                    {
                        RGDebug.LogDebug(
                            $"RGService UploadReplayData response received");
                        onSuccess.Invoke();
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to upload bot instance history replay data for bot instance id: {botInstanceId} - {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure();
            }
        }

        /**
         * Uploads a validation summary for a given bot instance
         */
        public async Task UploadValidationSummary(long botInstanceId, RGValidationSummary request, Action<RGBotInstanceHistory> onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebRequest(
                    uri: $"{GetRgServiceBaseUri()}/bot-instance-history/{botInstanceId}/validation-summary",
                    method: "POST",
                    payload: JsonUtility.ToJson(request),
                    onSuccess: (s) =>
                    {
                        // wrapper this as C#/Unity json can't handle top level arrays /yuck
                        RGBotInstanceHistory response = JsonUtility.FromJson<RGBotInstanceHistory>(s);
                        RGDebug.LogDebug(
                            $"RGService UploadValidationSummary response received: {response}");
                        onSuccess.Invoke(response);
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to upload a validation summary for bot instance {botInstanceId}: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure();
            }
        }

        /**
         * Uploads all validations for the bot instance to Regression Games in JSONL format
         */
        public async Task UploadValidations(long botInstanceId, string filePath, Action onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebFileUploadRequest(
                    uri: $"{GetRgServiceBaseUri()}/bot-instance-history/{botInstanceId}/validations",
                    method: "POST",
                    filePath: filePath,
                    contentType: "application/jsonl",
                    onSuccess: (s) =>
                    {
                        // wrapper this as C#/Unity json can't handle top level arrays /yuck
                        RGDebug.LogDebug(
                            $"RGService UploadValidations response received");
                        onSuccess.Invoke();
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to upload validations for bot instance {botInstanceId}: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure();
            }
        }

        /**
         * Uploads all log messages for the bot instance to Regression Games in JSONL format
         */
        public async Task UploadLogs(long botInstanceId, string filePath, Action onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebFileUploadRequest(
                    uri: $"{GetRgServiceBaseUri()}/bot-instance-history/{botInstanceId}/logs",
                    method: "POST",
                    filePath: filePath,
                    contentType: "application/jsonl",
                    onSuccess: (s) =>
                    {
                        RGDebug.LogDebug(
                            $"RGService UploadLogs response received");
                        onSuccess.Invoke();
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to upload logs for bot instance {botInstanceId}: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure();
            }
        }

        public async Task GetExternalConnectionInformationForBotInstance(long botInstanceId, Action<RGBotInstanceExternalConnectionInfo> onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebRequest(
                    uri: $"{GetRgServiceBaseUri()}/matchmaking/running-bot/{botInstanceId}/external-connection-info",
                    method: "GET",
                    payload: null,
                    onSuccess: (s) =>
                    {
                        RGBotInstanceExternalConnectionInfo connInfo =
                            JsonUtility.FromJson<RGBotInstanceExternalConnectionInfo>(s);
                        RGDebug.LogDebug($"RG Bot Instance external connection info: {connInfo}");
                        onSuccess.Invoke(connInfo);
                    },
                    onFailure: (f) => { onFailure.Invoke(); }
                );
            }
            else
            {
                onFailure();
            }
        }

        public async Task QueueInstantBot(long botId, Action<RGBotInstance> onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebRequest(
                    uri: $"{GetRgServiceBaseUri()}/matchmaking/instant-bot/queue",
                    method: "POST",
                    payload: JsonUtility.ToJson(new RGQueueInstantBotRequest("unused", 0, botId,
                        RG_UNITY_AUTH_TOKEN)), // TODO Remove host and port from payload if they're optional
                    onSuccess: (s) =>
                    {
                        RGBotInstance botInstance = JsonUtility.FromJson<RGBotInstance>(s);
                        RGBotServerListener.GetInstance()
                            .SetUnityBotState(botInstance.id, RGUnityBotState.STARTING);
                        RGDebug.LogInfo($"Bot Instance id: {botInstance.id} started");
                        onSuccess.Invoke(botInstance);
                    },
                    onFailure: (f) => { onFailure.Invoke(); }
                );
            }
            else
            {
                onFailure();
            }
        }

        public async Task GetRunningInstancesForBot(long botId, Action<RGBotInstance[]> onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebRequest(
                    uri: $"{GetRgServiceBaseUri()}/matchmaking/running-bot/{botId}",
                    method: "GET",
                    payload: null,
                    onSuccess: (s) =>
                    {
                        // wrapper this as C#/Unity json can't handle top level arrays /yuck
                        string theNewText = $"{{\"botInstances\":{s}}}";
                        // Using a different JSON library here to try to handle date time fields better
                        RGBotInstanceList botInstanceList = JsonConvert.DeserializeObject<RGBotInstanceList>(theNewText);
                        onSuccess.Invoke(botInstanceList.botInstances);
                    },
                    onFailure: (f) => { onFailure.Invoke(); }
                );
            }
            else
            {
                onFailure();
            }
        }

        public async Task StopBotInstance(long botInstanceId, Action onSuccess, Action onFailure)
        {
            if (await EnsureAuthed())
            {
                await SendWebRequest(
                    uri: $"{GetRgServiceBaseUri()}/matchmaking/running-bot/{botInstanceId}/stop",
                    method: "POST",
                    payload: null,
                    onSuccess: (s) => { onSuccess.Invoke(); },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to stop bot instance {botInstanceId}: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure();
            }
        }

        /**
         * MUST be called on main thread only... This is because `new UnityWebRequest` makes a .Create call internally
         */
        private Task SendWebRequest(
            string uri, string method, string payload, Action<string> onSuccess, Action<string> onFailure,
            bool isAuth = false, string contentType = "application/json")
            => SendWebRequest(uri, method, payload,
                s =>
                {
                    onSuccess(s);
                    return Task.CompletedTask;
                },
                s =>
                {
                    onFailure(s);
                    return Task.CompletedTask;
                },
                isAuth,
                contentType);

        private async Task SendWebRequest(string uri, string method, string payload, Func<string, Task> onSuccess, Func<string, Task> onFailure, bool isAuth=false, string contentType="application/json")
        {
            var messageId = ++correlationId;
            // don't log the details of auth requests :)
            string payloadToLog = isAuth ? "{***:***, ...}" : payload;
            RGDebug.LogVerbose($"<{messageId}> API request - {method}  {uri}{(!string.IsNullOrEmpty(payload)?$"\r\n{payloadToLog}":"")}");
            UnityWebRequest request = new UnityWebRequest(uri, method);

            try
            {
                var payloadBytes = payload == null ? null : Encoding.UTF8.GetBytes(payload);
                request.timeout = WEB_REQUEST_TIMEOUT_SECONDS;
                UploadHandler uh = new UploadHandlerRaw(payloadBytes);
                uh.contentType = "application/json";
                request.uploadHandler = uh;
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", contentType);
                request.SetRequestHeader("Accept", "application/json");
                if (rgAuthToken != null && !isAuth)
                {
                    request.SetRequestHeader("Authorization", $"Bearer {rgAuthToken}");
                }

                if (request.uri.Scheme.Equals(Uri.UriSchemeHttps))
                {
                    request.certificateHandler = RGCertOnlyPublicKey.GetInstance();
                }
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

        /**
         * MUST be called on main thread only... This is because `new UnityWebRequest` makes a .Create call internally
         */
        private Task SendWebFileUploadRequest(string uri, string method, string filePath, string contentType, Action<string> onSuccess, Action<string> onFailure)
            => SendWebFileUploadRequest(uri, method, filePath, contentType,
                s =>
                {
                    onSuccess(s);
                    return Task.CompletedTask;
                },
                s =>
                {
                    onFailure(s);
                    return Task.CompletedTask;
                }
            );

        private async Task SendWebFileUploadRequest(string uri, string method, string filePath, string contentType, Func<string, Task> onSuccess, Func<string, Task> onFailure)
        {
            var messageId = ++correlationId;
            RGDebug.LogVerbose($"<{messageId}> API request - {method}  {uri}\r\nfilePath:{filePath}");
            UnityWebRequest request = new UnityWebRequest(uri, method);

            try
            {
                request.timeout = WEB_REQUEST_TIMEOUT_SECONDS;
                UploadHandler uh = new UploadHandlerFile(filePath);
                uh.contentType = contentType;
                request.uploadHandler = uh;
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", contentType);
                request.SetRequestHeader("Accept", "application/json");
                if (rgAuthToken != null)
                {
                    request.SetRequestHeader("Authorization", $"Bearer {rgAuthToken}");
                }

                if (request.uri.Scheme.Equals(Uri.UriSchemeHttps))
                {
                    request.certificateHandler = RGCertOnlyPublicKey.GetInstance();
                }

                var task = request.SendWebRequest();
                RGDebug.LogVerbose($"<{messageId}> API file request sent ...");
                await new UnityWebRequestAwaiter(task);
                RGDebug.LogVerbose($"<{messageId}> API file request complete ...");

                string resultText = request.downloadHandler?.text;
                string resultToLog = resultText;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // pretty print
                    RGDebug.LogVerbose($"<{messageId}> API file response - {method}  {uri}\r\n{resultToLog}");
                    await onSuccess.Invoke(resultText);
                }
                else
                {
                    // since we call this method frequently waiting for bots to come online, we log this at a more debug level
                    string errorString =
                        $"<{messageId}> API file error - {method}  {uri}\r\n{request.error} - {request.result} - {resultToLog}";
                    RGDebug.LogDebug(errorString);
                    await onFailure.Invoke(errorString);
                }
            }
            catch (Exception ex)
            {
                // since we call this method frequently waiting for bots to come online, we log this at a more debug level
                string errorString =
                    $"<{messageId}> API file Exception - {method}  {uri} - {ex}";
                RGDebug.LogDebug(errorString);
                await onFailure.Invoke(errorString);
            }
            finally
            {
                request?.Dispose();
            }

        }

        /**
         * MUST be called on main thread only... This is because `new UnityWebRequest` makes a .Create call internally
         */
        private Task SendWebFileDownloadRequest(string uri, string method, string payload, string destinationFilePath, Action onSuccess, Action<string> onFailure)
            => SendWebFileDownloadRequest(uri, method, payload, destinationFilePath,
                () =>
                {
                    onSuccess();
                    return Task.CompletedTask;
                },
                s =>
                {
                    onFailure(s);
                    return Task.CompletedTask;
                }
            );

        private async Task SendWebFileDownloadRequest(string uri, string method, string payload, string destinationFilePath, Func<Task> onSuccess, Func<string, Task> onFailure)
        {
            var messageId = ++correlationId;
            RGDebug.LogVerbose($"<{messageId}> API request - {method}  {uri}");
            UnityWebRequest request = new UnityWebRequest(uri, method);

            try
            {
                var payloadBytes = payload == null ? null : Encoding.UTF8.GetBytes(payload);
                request.timeout = WEB_REQUEST_TIMEOUT_SECONDS;
                UploadHandler uh = new UploadHandlerRaw(payloadBytes);
                uh.contentType = "application/json";
                request.uploadHandler = uh;
                request.downloadHandler = new DownloadHandlerFile(destinationFilePath);
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accepts", "application/zip");
                if (rgAuthToken != null)
                {
                    request.SetRequestHeader("Authorization", $"Bearer {rgAuthToken}");
                }

                if (request.uri.Scheme.Equals(Uri.UriSchemeHttps))
                {
                    request.certificateHandler = RGCertOnlyPublicKey.GetInstance();
                }

                var task = request.SendWebRequest();
                RGDebug.LogVerbose($"<{messageId}> API file request sent ...");
                await new UnityWebRequestAwaiter(task);
                RGDebug.LogVerbose($"<{messageId}> API file request complete ...");

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // pretty print
                    RGDebug.LogVerbose($"<{messageId}> API file response - {method}  {uri}\r\ndestinationFilePath: {destinationFilePath}");
                    await onSuccess.Invoke();
                }
                else
                {
                    // since we call this method frequently waiting for bots to come online, we log this at a more debug level
                    string errorString =
                        $"<{messageId}> API file error - {method}  {uri}\r\n{request.error}";
                    RGDebug.LogDebug(errorString);
                    await onFailure.Invoke(errorString);
                }
            }
            catch (Exception ex)
            {
                // since we call this method frequently waiting for bots to come online, we log this at a more debug level
                string errorString =
                    $"<{messageId}> API file Exception - {method}  {uri} - {ex}";
                RGDebug.LogDebug(errorString);
                await onFailure.Invoke(errorString);
            }
            finally
            {
                request?.Dispose();
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
