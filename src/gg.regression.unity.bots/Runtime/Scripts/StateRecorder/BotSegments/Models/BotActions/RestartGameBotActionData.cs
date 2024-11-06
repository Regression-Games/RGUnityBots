using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using StateRecorder.BotSegments.Models;
using UnityEngine.SceneManagement;

namespace RegressionGames.StateRecorder.BotSegments.Models.BotActions
{
    [Serializable]
    public class RestartGameBotActionData : IBotActionData
    {
        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.RestartGame;

        private static IRGRestartGameAction _action;

        public int apiVersion = SdkApiVersion.VERSION_25;

        [NonSerialized]
        private bool _isStopped;

        private string _error;

        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            if (!_isStopped)
            {
                if (_action == null)
                {
                    // load the type on another thread to avoid 'hitching' the game
                    new Thread(() =>
                    {
                        // load our script type without knowing the assembly name, just the full type
                        var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                        var allAssembliesLength = allAssemblies.Length;
                        for (var i = 0; _action == null && i < allAssembliesLength; i++)
                        {
                            var a = allAssemblies[i];
                            foreach (var type in a.GetTypes())
                            {
                                if (typeof(IRGRestartGameAction).IsAssignableFrom(type))
                                {
                                    RGDebug.LogInfo($"Using the '{type.FullName}' implementation of IRGRestartGameAction to process Bot Restart Game Actions.  If this is not the type you expected to handle this action, you may accidentally have more than one implementation of the IRGRestartGameAction in your runtime.");
                                    _action = (IRGRestartGameAction)Activator.CreateInstance(type);
                                }
                                break;
                            }
                        }

                        if (_action == null)
                        {
                            _error = $"Regression Games could not find an IRGRestartGameAction implementation. The system will use the default restart action of SceneManager.LoadScene(sceneBuildIndex: 0, mode: LoadSceneMode.Single)";
                            RGDebug.LogWarning(_error);
                        }
                        else
                        {
                            _error = null;
                        }
                    }).Start();
                }
            }
        }

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            if (!_isStopped)
            {
                _isStopped = true;
                // find the restart interface impl in the runtime.. if it doesn't exist... use our default action
                // run the restart action
                if (_action != null)
                {
                    RGDebug.LogInfo($"Restarting the game using the '{_action.GetType().FullName}' implementation of IRGRestartGameAction.  If this is not the type you expected to handle this action, you may accidentally have more than one implementation of the IRGRestartGameAction in your runtime.");
                    try
                    {
                        _action.RestartGame();
                        _error = null;
                        error = _error;
                        return true;
                    }
                    catch (Exception e)
                    {
                        _error = "An exception occurred trying to processing the Bot RestartGame Action:\n" + e;
                        error = _error;
                        return false;
                    }
                }

                // else
                RGDebug.LogInfo($"Restarting the game using the default action of SceneManager.LoadScene(sceneBuildIndex: 0, mode: LoadSceneMode.Single)");
                // no restart impl, load scene 0 instead...
                // BEWARE.. This DOES NOT... cleanup background threads, destroy DontDestroyOnLoad objects, or cleanup many other non-game object associated things in the engine
                SceneManager.LoadScene(sceneBuildIndex: 0, mode: LoadSceneMode.Single);
                _error = null;
                error = _error;
                return true;

            }

            error = _error;
            return false;
        }

        public void AbortAction(int segmentNumber)
        {
            _isStopped = true;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append("}");
        }

        public void ReplayReset()
        {
            _isStopped = false;
        }

        public bool IsCompleted()
        {
            return _isStopped;
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
