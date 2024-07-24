using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.CVSerice;
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
            return "127.0.0.1:10080";
        }

        public async Task PostCriteriaTextDiscover(CVTextCriteriaRequest request, Action<Action> abortHook, Action<List<CVTextResult>> onSuccess, Action onFailure)
        {
            await SendWebRequest(
                uri: $"{GetCvServiceBaseUri()}/criteria-text-discover",
                method: "POST",
                payload: request.ToJsonString(),
                abortHook: abortHook.Invoke,
                onSuccess: (s) =>
                {
                    var response = JsonConvert.DeserializeObject<List<CVTextResult>>(s);
                    RGDebug.LogDebug(
                        $"CVService POST criteria-text-discover response received: {response}");
                    onSuccess.Invoke(response);
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
            string uri, string method, string payload, Action<Action> abortHook, Action<string> onSuccess, Action<string> onFailure,
            bool isAuth = false, string contentType = "application/json")
            => SendWebRequest(
                uri,
                method,
                payload,
                s =>
                {
                    abortHook(s);
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

        private async Task SendWebRequest(string uri, string method, string payload, Func<Action, Task> abortHook, Func<string, Task> onSuccess, Func<string, Task> onFailure, bool isAuth = false, string contentType = "application/json")
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
                await abortHook.Invoke(() => task.webRequest.Abort());
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

    }

}
