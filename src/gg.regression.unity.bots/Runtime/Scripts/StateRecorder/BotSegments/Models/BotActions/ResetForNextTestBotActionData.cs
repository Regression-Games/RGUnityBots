using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments.Models.BotActions
{
    /**
     * <summary>An action to reset key states in the game to prepare for the next test.  This should generally only be used as the 'last' segment in your sequence or segment list.</summary>
     */
    [Serializable]
    public class ResetForNextTestBotActionData : IBotActionData
    {
        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.ResetForNextTest;

        // intentionally static to avoid assembly lookup on each run
        private static IRGResetForNextTestAction _action;

        // NOT static so we get a new copy each run
        private RestartGameBotActionData _restartGameAction = null;

        public int apiVersion = SdkApiVersion.VERSION_25;

        [NonSerialized]
        private bool _isStopped;

        private string _error;

        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            if (!_isStopped)
            {
                if (_action == null && _restartGameAction == null)
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
                                if (typeof(IRGResetForNextTestAction).IsAssignableFrom(type) && !type.IsInterface)
                                {
                                    RGDebug.LogInfo($"Using the '{type.FullName}' implementation of IRGResetForNextTestAction to process Bot ResetForNextTest Actions.  If this is not the type you expected to handle this action, you may accidentally have more than one implementation of the IRGResetForNextTestAction interface in your runtime.");
                                    _action = (IRGResetForNextTestAction)Activator.CreateInstance(type);
                                    break;
                                }
                            }
                        }

                        if (_action == null)
                        {
                            _error = null;
                            RGDebug.LogWarning("Regression Games could not find an IRGResetForNextTestAction implementation. The system will default to using the RestartGame Bot Action");
                            _restartGameAction = new RestartGameBotActionData();
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
                // use the resetfornexttest interface impl in the runtime.. if it doesn't exist... use our default action
                // run the resetfornexttest action
                if (_action != null)
                {
                    RGDebug.LogInfo($"Resetting the game state for the next test using the '{_action.GetType().FullName}' implementation of IRGResetForNextTestAction.  If this is not the type you expected to handle this action, you may accidentally have more than one implementation of the IRGResetForNextTestAction interface in your runtime.");
                    try
                    {
                        _isStopped = true;
                        _action.ResetForNextTest();
                        _error = null;
                        error = _error;
                        return true;
                    }
                    catch (Exception e)
                    {
                        _error = "An exception occurred trying to processing the Bot ResetForNextTest Action:\n" + e;
                        error = _error;
                        return false;
                    }
                }

                // else
                // no reset impl, run the restart logic instead
                if (_restartGameAction != null)
                {
                    RGDebug.LogInfo($"Resetting the game state for the next test using the RestartGame Bot Action.  If this is not what you expected, you most likely do not have an implementation of the IRGResetForNextTestAction interface in your runtime.");
                    _restartGameAction.StartAction(segmentNumber, currentTransforms, currentEntities);
                    var didRestart = _restartGameAction.ProcessAction(segmentNumber, currentTransforms, currentEntities, out error);
                    _isStopped |= didRestart;
                    _error = error;
                    return didRestart;
                }
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
