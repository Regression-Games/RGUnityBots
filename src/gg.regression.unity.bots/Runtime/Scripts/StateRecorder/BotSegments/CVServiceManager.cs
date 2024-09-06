using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.Models.CVService;
using UnityEngine.Networking;

namespace RegressionGames
{
    public class CVServiceManager
    {

        private static readonly int WEB_REQUEST_TIMEOUT_SECONDS = 30;

        private static readonly CVServiceManager _this = new ();

        private long _correlationId = 0L;

        private readonly string _cvAuthToken = "TODO:GenerateADeploymentTokenOnReleaseAndReadFromAnArgument";

        public static CVServiceManager GetInstance()
        {
            return _this;
        }

        private String GetCvServiceBaseUri()
        {
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            string host = rgSettings.GetAIServiceHostAddress();

            // If env var is set, use that instead
            string hostOverride = RGEnvConfigs.ReadCvHost();
            if (hostOverride != null && hostOverride.Trim() != "")
            {
                host = hostOverride.Trim();
            }

            if (host.EndsWith('/'))
            {
                host = host.Substring(0, host.Length - 1);
            }

            Uri hostUri = new(host);
            if (!hostUri.IsLoopback)
            {
                return $"{host}/aiservice";
            }
            return $"{host}";
        }

        public async Task PostCriteriaImageMatch(CVImageCriteriaRequest request, Action<Action> abortRegistrationHook, Action<List<CVImageResult>> onSuccess, Action onFailure)
        {
            await SendWebRequest(
                uri: $"{GetCvServiceBaseUri()}/criteria-image-match",
                method: "POST",
                payload: request.ToJsonString(),
                abortRegistrationHook: abortRegistrationHook.Invoke,
                onSuccess: (s) =>
                {
                    var response = JsonConvert.DeserializeObject<CVImageResultList>(s);
                    onSuccess.Invoke(response.results);
                },
                onFailure: (f) =>
                {
                    RGDebug.LogWarning($"Failed to evaluate CV Image criteria: {f}");
                    onFailure.Invoke();
                }
            );
        }

        public async Task PostCriteriaObjectDetection(CVObjectDetectionRequest request,
                                                      Action<Action> abortRegistrationHook,
                                                      Action<List<CVObjectDetectionResult>> onSuccess,
                                                      Action onFailure)
        {

            string endpoint;
            if (!string.IsNullOrEmpty(request.textQuery))
            {
                endpoint = "criteria-object-text-query";
            }
            else if (request.imageQuery != null)
            {
                endpoint = "criteria-object-image-query";
            }
            else if (request.imageQuery != null && request.textQuery != null){
                RGDebug.LogError("Invalid CVObjectDetectionRequest: Both textQuery and queryImage are both set. Please set one or the other.");
                onFailure.Invoke();
                return;
            }
            else
            {
                RGDebug.LogError("Invalid CVObjectDetectionRequest: Both textQuery and queryImage are null or empty. Please set at least one of them.");
                onFailure.Invoke();
                return;
            }

            await SendWebRequest(
                uri: $"{GetCvServiceBaseUri()}/{endpoint}",
                method: "POST",
                payload: request.ToJsonString(),
                abortRegistrationHook: abortRegistrationHook.Invoke,
                onSuccess: (s) =>
                {
                    var response = JsonConvert.DeserializeObject<CVObjectDetectionResultList>(s);
                    onSuccess.Invoke(response.results);
                },
                onFailure: (f) =>
                {
                    RGDebug.LogWarning($"Failed to evaluate CV Object Detection via Text Query criteria: {f}");
                    onFailure.Invoke();
                }
            );
        }

        public async Task PostCriteriaTextDiscover(CVTextCriteriaRequest request, Action<Action> abortRegistrationHook, Action<List<CVTextResult>> onSuccess, Action onFailure)
        {
            await SendWebRequest(
                uri: $"{GetCvServiceBaseUri()}/criteria-text-discover",
                method: "POST",
                payload: request.ToJsonString(),
                abortRegistrationHook: abortRegistrationHook.Invoke,
                onSuccess: (s) =>
                {
                    var response = JsonConvert.DeserializeObject<CVTextResultList>(s);
                    onSuccess.Invoke(response.results);
                },
                onFailure: (f) =>
                {
                    RGDebug.LogWarning($"Failed to evaluate CV Text criteria: {f}");
                    onFailure.Invoke();
                }
            );
        }

        /**
         * MUST be called on main thread only... This is because `new UnityWebRequest` makes a .Create call internally
         */
        private Task SendWebRequest(
            string uri, string method, string payload, Action<Action> abortRegistrationHook, Action<string> onSuccess, Action<string> onFailure,
            bool isAuth = false, string contentType = "application/json")
            => SendWebRequest(
                uri,
                method,
                payload,
                s =>
                {
                    abortRegistrationHook(s);
                    return Task.CompletedTask;
                },
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

        private async Task SendWebRequest(string uri, string method, string payload, Func<Action, Task> abortRegistrationHook, Func<string, Task> onSuccess, Func<string, Task> onFailure, bool isAuth = false, string contentType = "application/json")
        {
            var messageId = ++_correlationId;
            // don't log the details of auth requests :)
            string payloadToLog = isAuth ? "{***:***, ...}" : payload;
            RGDebug.LogDebug($"<{messageId}> API request - {method}  {uri}{(!string.IsNullOrEmpty(payload) ? $"\r\n{payloadToLog}" : "")}");
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
                if (_cvAuthToken != null && !isAuth)
                {
                    request.SetRequestHeader("Authorization", $"Bearer {_cvAuthToken}");
                }

                if (request.uri.Scheme.Equals(Uri.UriSchemeHttps))
                {
                    request.certificateHandler = RGCertOnlyPublicKey.GetInstance();
                }
                var task = request.SendWebRequest();
                // pass them back a hook to abort this request
                await abortRegistrationHook.Invoke(() =>
                {
                    try
                    {
                        if (!task.webRequest.isDone)
                        {
                            task.webRequest.Abort();
                        }
                    }
                    catch (Exception)
                    {
                        // we tried to help
                    }
                });
                RGDebug.LogVerbose($"<{messageId}> API request sent ...");
                await new UnityWebRequestAwaiter(task);
                var responseCode = task.webRequest.responseCode;
                RGDebug.LogVerbose($"<{messageId}> API request complete ({responseCode})...");

                string resultText = request.downloadHandler?.text;
                string resultToLog = isAuth ? "{***:***, ...}" : resultText;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // pretty print
                    RGDebug.LogDebug($"<{messageId}> API response - {method}  {uri}\r\n{resultToLog}");
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

    }

}
