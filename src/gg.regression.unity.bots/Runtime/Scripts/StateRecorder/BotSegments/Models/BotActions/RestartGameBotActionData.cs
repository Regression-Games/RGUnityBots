using System;
using System.Collections.Generic;
using System.Text;
// ReSharper disable once RedundantUsingDirective - used in #else.. don't remove
using System.Threading;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using StateRecorder.BotSegments.Models;
using UnityEngine;
// ReSharper disable once RedundantUsingDirective - used in #else.. don't remove
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
// ReSharper disable InconsistentNaming
#endif
namespace RegressionGames.StateRecorder.BotSegments.Models.BotActions
{
    /**
     * <summary>An action to restart the game.  This should only be used as the 'last' segment in your sequence or segment list until REG-2170.</summary>
     */
    [Serializable]
    public class RestartGameBotActionData : IBotActionData
    {
        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.RestartGame;

        private static IRGRestartGameAction _action;

        public int apiVersion = SdkApiVersion.VERSION_25;

        private static readonly string _restartWarningText = "the default restart action of destroying DontDestroyOnLoad objects and then running SceneManager.LoadScene(sceneBuildIndex: 0, mode: LoadSceneMode.Single).  Please note that this should stop all co-routines, but this will NOT stop any background threads, tasks, timers, Unity Jobs, etc.  This will also NOT cleanup any data in ECS.";

        [NonSerialized]
        private bool _isStopped;

        private volatile bool _readyToProcess;

        private string _error;

        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            if (!_isStopped)
            {
                if (!_readyToProcess && _action == null)
                {
#if UNITY_EDITOR
                    // no-op
                    _readyToProcess = true;
#else

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
                                if (typeof(IRGRestartGameAction).IsAssignableFrom(type) && !type.IsInterface)
                                {
                                    RGDebug.LogInfo($"Using the '{type.FullName}' implementation of IRGRestartGameAction to process Bot Restart Game Actions.  If this is not the type you expected to handle this action, you may accidentally have more than one implementation of the IRGRestartGameAction interface in your runtime.");
                                    _action = (IRGRestartGameAction)Activator.CreateInstance(type);
                                    break;
                                }
                            }
                        }

                        if (_action == null)
                        {
                            RGDebug.LogWarning("Regression Games could not find an IRGRestartGameAction implementation. The system will use " + _restartWarningText);
                        }
                        _error = null;
                        _readyToProcess = true;
                    }).Start();
#endif
                }
            }
        }

#if UNITY_EDITOR
        private void PlayGameInEditor(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                // un-register our listener
                EditorApplication.playModeStateChanged -= PlayGameInEditor;
                // just finished playing ... start it back up
                EditorApplication.isPlaying = true;
            }
        }

#endif

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            if (!_isStopped && _readyToProcess)
            {
                //TODO (REG-2170): Write a status to persistent path so that we can resume this sequence/segment on game restart
                //TODO (REG-2170): (in code somewhere else ...) If this is NOT the last segment, block the upload from happening yet as we aren't 'done' recording the replay
                //TODO (REG-2170): (in code somewhere else ...) Read recovery status from persistent path so that we can resume this sequence/segment on game restart
                
                // use the restart interface impl in the runtime.. if it doesn't exist... use our default action
                // run the restart action
                if (_action != null)
                {
                    RGDebug.LogInfo($"Restarting the game using the '{_action.GetType().FullName}' implementation of IRGRestartGameAction.  If this is not the type you expected to handle this action, you may accidentally have more than one implementation of the IRGRestartGameAction interface in your runtime.");
                    try
                    {
                        _isStopped = true;
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
                // no restart impl, load scene 0 instead if this is in a build, and restart in the editor otherwise
                
#if UNITY_EDITOR
                _isStopped = true;
                RGDebug.LogInfo($"Restarting the game in the editor...");
                _error = null;
                error = _error;
                // register our hook so that the game will start right back up
                EditorApplication.playModeStateChanged += PlayGameInEditor;
                // stop the game in the editor
                EditorApplication.isPlaying = false;
                return true;
#else
                // BEWARE.. This DOES NOT... cleanup background threads or cleanup many other non-game object associated things in the engine
                _isStopped = true;
                RGDebug.LogInfo($"Restarting the game using " + _restartWarningText);
                DestroyAllDontDestroyOnLoadObjects();
                SceneManager.LoadScene(sceneBuildIndex: 0, mode: LoadSceneMode.Single);
                _error = null;
                error = _error;
                return true;
#endif
            }

            error = _error;
            return false;
        }

        private void DestroyAllDontDestroyOnLoadObjects()
        {

            GameObject temp = null;
            try
            {
                temp = new GameObject();
                UnityEngine.Object.DontDestroyOnLoad( temp );
                UnityEngine.SceneManagement.Scene dontDestroyOnLoad = temp.scene;

                // gets all don't destroy on load object
                var ddolgos = dontDestroyOnLoad.GetRootGameObjects();
                foreach (var gameObject in ddolgos)
                {
                    // make sure we only destroy temp once and leave the RG overlay
                    if (gameObject != temp && !gameObject.transform.name.StartsWith("RGOverlayCanvas"))
                    {
                        UnityEngine.Object.Destroy(gameObject);
                    }
                }

                UnityEngine.Object.Destroy( temp );
                temp = null;
            }
            finally
            {
                if (temp != null)
                {
                    UnityEngine.Object.Destroy( temp );
                }
            }

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
