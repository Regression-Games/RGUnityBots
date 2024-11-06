using System;
using System.Collections.Generic;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder.BotSegments.Models.BotActions
{

    /**
     * <summary>An action to quite the game.  This should only be used as the 'last' segment in your sequence or segment list.</summary>
     */
    [Serializable]
    public class QuitGameBotActionData : IBotActionData
    {
        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.QuitGame;

        public int apiVersion = SdkApiVersion.VERSION_25;

        [NonSerialized]
        private bool _isStopped;


        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            //no-op
        }

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            if (!_isStopped)
            {
                _isStopped = true;
                error = null;

#if UNITY_EDITOR
                RGDebug.LogInfo($"Stopping the game in the editor...  If your active bot sequence or segment list had more segments defined after this.  They will NOT run.");
                UnityEditor.EditorApplication.isPlaying = false;
#else
                RGDebug.LogInfo($"Stopping the game process.  If your active bot sequence or segment list had more segments defined after this.  They will NOT run.");
                Application.Quit();
#endif
                return true;
            }

            error = null;
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
