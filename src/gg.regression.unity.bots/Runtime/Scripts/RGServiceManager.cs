using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RegressionGames.RemoteOrchestration.Types;
using RegressionGames.Types;
using UnityEngine;
using UnityEngine.Networking;

namespace RegressionGames
{
    [HelpURL("https://docs.regression.gg/creating-bots/csharp/adaptive-bots")]
    public class RGServiceManager
    {

        // 10 seconds
        public static readonly int WEB_REQUEST_TIMEOUT_SECONDS = 10;

        // 1 hour
        public static readonly int WEB_REQUEST_FILE_TIMEOUT_SECONDS = 60 * 60;

        private string _rgAuthToken;

        private static readonly RGServiceManager _this = new();

        private long _correlationId = 0L;

        private RGServiceManager()
        {
        }

        public static RGServiceManager GetInstance()
        {
            return _this;
        }

        public bool IsAuthed()
        {
            return _rgAuthToken != null;
        }

        public bool LoadAuth(out string authToken)
        {
            try
            {
                // If an API key was given, just return and set that
                var apiKey = RGEnvConfigs.ReadAPIKey();
                if (apiKey != null && apiKey.Trim() != "")
                {
                    RGDebug.LogDebug("Using API Key from env var rather than from built in resources");
                    _rgAuthToken = apiKey.Trim();
                }
                else
                {
                    RGSettings settings = RGSettings.GetOrCreateSettings();
                    RGDebug.LogDebug("Using API Key from RGSettings resource");

                    var apiKeyFromSettings = settings.GetApiKey();
                    if (!string.IsNullOrEmpty(apiKeyFromSettings))
                    {
                        _rgAuthToken = apiKeyFromSettings;
                    }
                    else
                    {
                        RGDebug.LogWarning("RG API Key not configured");
                        authToken = _rgAuthToken;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                RGDebug.LogException(ex);
            }

            authToken = _rgAuthToken;
            return IsAuthed();
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

        public async Task GetBotsForCurrentUser(Action<RGBot[]> onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
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
                onFailure.Invoke();
            }
        }

        public async Task CreateBot(RGCreateBotRequest request, Action<RGBot> onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
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
                onFailure.Invoke();
            }
        }

        public async Task GetBotCodeDetails(long botId, Action<RGBotCodeDetails> onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
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
                onFailure.Invoke();
            }
        }

        public async Task UpdateBotCode(long botId, string filePath, Action<RGBotCodeDetails> onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
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
                onFailure.Invoke();
            }
        }

        public async Task DownloadBotCode(long botId, string destinationFilePath, Action onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
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
                onFailure.Invoke();
            }
        }

        public async Task CreateGameplaySession(DateTime startTime, DateTime endTime, long numTicks, long loggedWarnings, long loggedErrors, Action<RGGameplaySession> onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
            {
                RGGameplaySessionCreateRequest request = new RGGameplaySessionCreateRequest(startTime, endTime, numTicks, loggedWarnings, loggedErrors);
                await SendWebRequest(
                    uri: $"{GetRgServiceBaseUri()}/gameplay-session",
                    method: "POST",
                    payload: JsonConvert.SerializeObject(request),
                    onSuccess: (s) =>
                    {
                        RGGameplaySession response = JsonUtility.FromJson<RGGameplaySession>(s);
                        RGDebug.LogDebug(
                            $"RGService CreateGameplaySession response received");
                        onSuccess.Invoke(response);
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to create gameplay session: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure.Invoke();
            }
        }

        public async Task UploadGameplaySessionBotSegments(long gameplaySessionId, string zipPath, Action onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
            {
                await SendWebFileUploadRequest(
                    uri: $"{GetRgServiceBaseUri()}/gameplay-session/{gameplaySessionId}/bot-segments",
                    method: "POST",
                    filePath: zipPath,
                    contentType: "application/zip",
                    onSuccess: (s) =>
                    {
                        RGDebug.LogDebug(
                            $"RGService GameplaySessionData response received");
                        onSuccess.Invoke();
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to upload bot_segments for gameplay session {gameplaySessionId}: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure.Invoke();
            }
        }
        public async Task UploadGameplaySessionData(long gameplaySessionId, string zipPath, Action onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
            {
                await SendWebFileUploadRequest(
                    uri: $"{GetRgServiceBaseUri()}/gameplay-session/{gameplaySessionId}/data",
                    method: "POST",
                    filePath: zipPath,
                    contentType: "application/zip",
                    onSuccess: (s) =>
                    {
                        RGDebug.LogDebug(
                            $"RGService GameplaySessionData response received");
                        onSuccess.Invoke();
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to upload data for gameplay session {gameplaySessionId}: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure.Invoke();
            }
        }

        public async Task UploadGameplaySessionScreenshots(long gameplaySessionId, string zipPath, Action onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
            {
                await SendWebFileUploadRequest(
                    uri: $"{GetRgServiceBaseUri()}/gameplay-session/{gameplaySessionId}/screenshots",
                    method: "POST",
                    filePath: zipPath,
                    contentType: "application/zip",
                    onSuccess: (s) =>
                    {
                        RGDebug.LogDebug(
                            $"RGService GameplaySessionScreenshots response received");
                        onSuccess.Invoke();
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to upload screenshots for gameplay session {gameplaySessionId}: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure.Invoke();
            }
        }

        public async Task UploadGameplaySessionThumbnail(long gameplaySessionId, string jpegPath, Action onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
            {
                await SendWebFileUploadRequest(
                    uri: $"{GetRgServiceBaseUri()}/gameplay-session/{gameplaySessionId}/thumbnail",
                    method: "POST",
                    filePath: jpegPath,
                    contentType: "image/jpeg",
                    onSuccess: (s) =>
                    {
                        RGDebug.LogDebug(
                            $"RGService GameplaySessionThumbnail response received");
                        onSuccess.Invoke();
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to upload thumbnail for gameplay session {gameplaySessionId}: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure.Invoke();
            }
        }

        public async Task UploadGameplaySessionMetadata(long gameplaySessionId, string metadataPath, Action onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
            {
                await SendWebFileUploadRequest(
                    uri: $"{GetRgServiceBaseUri()}/gameplay-session/{gameplaySessionId}/metadata",
                    method: "POST",
                    filePath: metadataPath,
                    contentType: "application/json",
                    onSuccess: (s) =>
                    {
                        RGDebug.LogDebug(
                            $"RGService GameplaySessionMetadata response received");
                        onSuccess.Invoke();
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to upload metadata for gameplay session {gameplaySessionId}: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure.Invoke();
            }
        }

        public async Task UploadGameplaySessionLogs(long gameplaySessionId, string zipPath, Action onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
            {
                await SendWebFileUploadRequest(
                    uri: $"{GetRgServiceBaseUri()}/gameplay-session/{gameplaySessionId}/logs",
                    method: "POST",
                    filePath: zipPath,
                    contentType: "application/zip",
                    onSuccess: (s) =>
                    {
                        RGDebug.LogDebug($"RGService GameplaySessionLogs response received");
                        onSuccess.Invoke();
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed to upload logs for gameplay session {gameplaySessionId}: {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure.Invoke();
            }
        }

        public async Task SendRemoteWorkerHeartbeat(SDKClientHeartbeatRequest request, Action<SDKClientHeartbeatResponse> onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
            {
                await SendWebRequest(
                    uri: $"{GetRgServiceBaseUri()}/sdk-clients/heartbeat",
                    method: "POST",
                    payload: JsonConvert.SerializeObject(request),
                    onSuccess: (s) =>
                    {
                        SDKClientHeartbeatResponse response = JsonUtility.FromJson<SDKClientHeartbeatResponse>(s);
                        RGDebug.LogDebug(
                            $"RGService SDKClientHeartbeatResponse received: {s}");
                        onSuccess.Invoke(response);
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed SDKClient heartbeat - {f}");
                        onFailure.Invoke();
                    }
                );
            }
            else
            {
                onFailure.Invoke();
            }
        }

        public async Task SendRemoteWorkerRegistration(SDKClientRegistrationRequest request, Action<SDKClientRegistrationResponse> onSuccess, Action onFailure)
        {
            if (LoadAuth(out _))
            {
                await SendWebRequest(
                    uri: $"{GetRgServiceBaseUri()}/sdk-clients/register",
                    method: "POST",
                    payload: JsonConvert.SerializeObject(request),
                    onSuccess: (s) =>
                    {
                        SDKClientRegistrationResponse response = JsonUtility.FromJson<SDKClientRegistrationResponse>(s);
                        RGDebug.LogDebug(
                            $"RGService SDKClientRegistrationResponse received: {s}");
                        onSuccess.Invoke(response);
                    },
                    onFailure: (f) =>
                    {
                        RGDebug.LogWarning($"Failed SDKClient registration - {f}");
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
         * MUST be called on main thread only... This is because `new UnityWebRequest` makes a .Create call internally
         */
        private Task SendWebRequest(
            string uri, string method, string payload, Action<string> onSuccess, Action<string> onFailure,
            bool isAuth = false, string contentType = "application/json")
            => SendWebRequest(
                uri,
                method,
                payload,
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

        private async Task SendWebRequest(string uri, string method, string payload, Func<string, Task> onSuccess, Func<string, Task> onFailure, bool isAuth = false, string contentType = "application/json")
        {
            var messageId = ++_correlationId;
            // don't log the details of auth requests :)
            string payloadToLog = isAuth ? "{***:***, ...}" : payload;
            RGDebug.LogVerbose($"<{messageId}> API request - {method}  {uri}{(!string.IsNullOrEmpty(payload) ? $"\r\n{payloadToLog}" : "")}");
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
                if (_rgAuthToken != null && !isAuth)
                {
                    request.SetRequestHeader("Authorization", $"Bearer {_rgAuthToken}");
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
            var messageId = ++_correlationId;
            RGDebug.LogVerbose($"<{messageId}> API request - {method}  {uri}\r\nfilePath:{filePath}");
            UnityWebRequest request = new UnityWebRequest(uri, method);

            try
            {
                request.timeout = WEB_REQUEST_FILE_TIMEOUT_SECONDS;
                UploadHandler uh = new UploadHandlerFile(filePath);
                uh.contentType = contentType;
                request.uploadHandler = uh;
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", contentType);
                request.SetRequestHeader("Accept", "application/json");
                if (_rgAuthToken != null)
                {
                    request.SetRequestHeader("Authorization", $"Bearer {_rgAuthToken}");
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
            var messageId = ++_correlationId;
            RGDebug.LogVerbose($"<{messageId}> API request - {method}  {uri}");
            UnityWebRequest request = new UnityWebRequest(uri, method);

            try
            {
                var payloadBytes = payload == null ? null : Encoding.UTF8.GetBytes(payload);
                request.timeout = WEB_REQUEST_FILE_TIMEOUT_SECONDS;
                UploadHandler uh = new UploadHandlerRaw(payloadBytes);
                uh.contentType = "application/json";
                request.uploadHandler = uh;
                request.downloadHandler = new DownloadHandlerFile(destinationFilePath);
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accepts", "application/zip");
                if (_rgAuthToken != null)
                {
                    request.SetRequestHeader("Authorization", $"Bearer {_rgAuthToken}");
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


}
