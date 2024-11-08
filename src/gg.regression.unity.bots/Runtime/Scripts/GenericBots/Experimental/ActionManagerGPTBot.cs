using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.ActionManager;
using UnityEngine;
using UnityEngine.Networking;

namespace RegressionGames.GenericBots.Experimental
{
    /// <summary>
    /// Simple LLM bot that uses the action manager to generate an LLM prompt
    /// indicating the game rules in terms of the possible actions. Given a specified context and goal,
    /// GPT is prompted to generate a sequence of discrete actions to take. The action manager is then again
    /// used to interpret the response into a sequence of actual actions to take in game.
    /// </summary>
    public class ActionManagerGPTBot : MonoBehaviour
    {
        /// <summary>
        /// Context to provide to the agent about the game.
        /// </summary>
        public string Context = "You are playtesting a video game.";

        /// <summary>
        /// Description of the task to perform.
        /// </summary>
        public string Task = "Conduct exploratory testing like a quality assurance tester.";

        /// <summary>
        /// Number of discrete actions to generate.
        /// </summary>
        public int NumActions = 50;

        /// <summary>
        /// Rate at which to evaluate the generated actions.
        /// </summary>
        public float ActionInterval = 0.5f;

        /// <summary>
        /// Name of the GPT model available from the OpenAI API ("gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-4", "gpt-3.5-turbo", etc.)
        /// </summary>
        public string GPTModel = "gpt-4o-mini";

        /// <summary>
        /// OpenAI API key (must be provided by the user and kept secret)
        /// </summary>
        public string OpenAIApiKey = null;

        public void Start()
        {
            if (!RGActionManager.IsAvailable)
            {
                RGDebug.LogError("ActionManagerGPTBot - Action manager is currently unavailable. Have you run Regression Games > Configure Bot Actions > Analyze Actions on your project?");
                Destroy(this);
                return;
            }
            if (OpenAIApiKey == null)
            {
                RGDebug.LogError("ActionManagerGPTBot - OpenAI API key not set - please set this prior to using this bot");
                Destroy(this);
                return;
            }

            DontDestroyOnLoad(this);

            RGActionManager.StartSession(0,this);

            StartCoroutine("LLMAgent");
        }

        IEnumerator LLMAgent()
        {
            string systemPrompt = GenerateSystemPrompt();
            string userPrompt = GenerateUserPrompt();
            var requestBody = JsonConvert.SerializeObject(new
            {
                model = GPTModel,
                messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = systemPrompt
                    },
                    new
                    {
                        role = "user",
                        content = userPrompt
                    }
                }
            });
            RGDebug.LogInfo("ActionManagerGPTBot - Sending prompt to GPT:\n" + systemPrompt + "\n\n" + userPrompt);

            float prevTimeScale = Time.timeScale;
            Time.timeScale = 0.0f; // pause the game while waiting for a response

            using (var webRequest = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST"))
            {
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", "Bearer " + OpenAIApiKey);
                webRequest.uploadHandler = new UploadHandlerRaw(new UTF8Encoding().GetBytes(requestBody));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                yield return webRequest.SendWebRequest();

                Time.timeScale = prevTimeScale;

                UnityWebRequest.Result res = webRequest.result;
                if (res == UnityWebRequest.Result.Success)
                {
                    JObject result = JObject.Parse(webRequest.downloadHandler.text);
                    string response = result["choices"][0]["message"]["content"].ToString();
                    RGDebug.LogInfo("ActionManagerGPTBot - GPT response:\n" + response);
                    yield return EvaluateResponse(response);
                    RGDebug.LogInfo("ActionManagerGPTBot - Done!");
                    Destroy(this);
                }
                else
                {
                    RGDebug.LogError("ActionManagerGPTBot - OpenAI request failed, aborting");
                    Destroy(this);
                    yield break;
                }
            }
        }

        IEnumerator EvaluateResponse(string response)
        {
            string[] lines = response.Split("\n");
            int lineIndex = 0;
            while (!lines[lineIndex].Contains("!!START!!") && lineIndex < lines.Length)
                ++lineIndex;

            ++lineIndex;
            while (lineIndex < lines.Length)
            {
                string line = lines[lineIndex].Trim();
                if (line.Contains("!!END!!"))
                {
                    // done
                    break;
                }

                // strip any leading number prefix
                int actionNameStartIndex = 0;
                while (actionNameStartIndex < line.Length)
                {
                    char c = line[actionNameStartIndex];
                    if (char.IsDigit(c) || char.IsWhiteSpace(c) || c == '.')
                    {
                        ++actionNameStartIndex;
                    }
                    else
                    {
                        break;
                    }
                }
                string actionResponseNormalized = line.Substring(actionNameStartIndex).ToLower().Trim();

                IRGGameActionInstance matchedActionInst = null;
                object matchedParam = null;

                bool anyActions = false;
                foreach (var actionInst in RGActionManager.GetValidActions())
                {
                    anyActions = true;
                    var action = actionInst.BaseAction;
                    foreach (var param in GetDiscreteParamValues(action))
                    {
                        if (actionInst.IsValidParameter(param))
                        {
                            string actionNameNormalized = (action.DisplayName + " (" + param + ")").ToLower().Trim();
                            if (actionResponseNormalized == actionNameNormalized)
                            {
                                matchedActionInst = actionInst;
                                matchedParam = param;
                                break;
                            }
                        }
                    }
                }

                if (anyActions)
                {
                    if (matchedActionInst != null)
                    {
                        RGDebug.Log($"ActionManagerGPTBot - Matched response \"{line}\" to action: " + matchedActionInst.BaseAction.DisplayName + " (" + matchedParam + ")");
                        foreach (var inp in matchedActionInst.GetInputs(matchedParam))
                        {
                            inp.Perform(0);
                        }
                    }
                    else
                    {
                        RGDebug.LogWarning("ActionManagerGPTBot - GPT response did not match any valid actions (skipping): " + line);
                    }
                    ++lineIndex;
                }

                yield return new WaitForSeconds(ActionInterval);
            }
        }

        public void OnDestroy()
        {
            RGActionManager.StopSession();
        }

        private IEnumerable<object> GetDiscreteParamValues(RGGameAction act)
        {
            if (act.ParameterRange is RGDiscreteValueRange discRange)
            {
                for (int i = 0, n = discRange.NumValues; i < n; ++i)
                {
                    var val = discRange[i];
                    yield return val;
                }
            } else if (act.ParameterRange is RGContinuousValueRange contRange)
            {
                var ranges = contRange.Discretize(4);
                for (int i = 0, n = ranges.Length; i < n; ++i)
                {
                    var val = ranges[i].MidPoint;
                    yield return val;
                }
            }
        }

        private string GenerateSystemPrompt()
        {
            StringBuilder promptBuilder = new StringBuilder();
            promptBuilder.AppendLine(Context);
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("You have the following set of discrete actions:");

            int counter = 1;
            foreach (var action in RGActionManager.Actions)
            {
                foreach (var param in GetDiscreteParamValues(action))
                {
                    promptBuilder.AppendLine(counter + ". " + action.DisplayName + " (" + param + ")");
                    ++counter;
                }
            }

            return promptBuilder.ToString();
        }

        private string GenerateUserPrompt()
        {
            StringBuilder promptBuilder = new StringBuilder();

            promptBuilder.AppendLine("Your task is the following:");
            promptBuilder.AppendLine(Task);

            promptBuilder.AppendLine();

            promptBuilder.AppendLine("Generate a list of " + (NumActions+2) +
                                     " actions to perform at each step of the game in order to accomplish this task, choosing from the list given above. " +
                                     "At each step, pick a single action only. Only indicate the full name of the action as listed above." +
                                     "The first item of the list should be \"!!START!!\" and the last item should be \"!!END!!\".");

            return promptBuilder.ToString();
        }
    }
}
