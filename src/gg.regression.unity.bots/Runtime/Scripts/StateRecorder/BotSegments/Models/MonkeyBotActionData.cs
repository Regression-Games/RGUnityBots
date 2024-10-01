using System;
using System.Collections.Generic;
using System.Text;
using RegressionGames.ActionManager;
using RegressionGames.GenericBots;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [Serializable]
    public class MonkeyBotActionData : IBotActionData
    {
        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.MonkeyBot;

        public int apiVersion = SdkApiVersion.VERSION_6;
        public float actionInterval = 0.05f;
        public RGActionManagerSettings actionSettings;

        [NonSerialized]
        private bool _isStopped;

        [NonSerialized]
        private RGMonkeyBotLogic _monkey;

        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            var controller = UnityEngine.Object.FindObjectOfType<BotSegmentsPlaybackController>();
            RGActionManager.StartSession(controller, actionSettings);
            _monkey = new RGMonkeyBotLogic();
            _monkey.ActionInterval = actionInterval;
        }

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            if (!_isStopped)
            {
                bool didAnyAction = _monkey.Update();
                error = null;
                return didAnyAction;
            }

            error = null;
            return false;
        }

        public void AbortAction(int segmentNumber)
        {
            RGActionManager.StopSession();
            _isStopped = true;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"actionInterval\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, actionInterval);
            stringBuilder.Append(",\"actionSettings\":");
            actionSettings.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }

        public void ReplayReset()
        {
            _isStopped = false;
            _monkey = null;
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
