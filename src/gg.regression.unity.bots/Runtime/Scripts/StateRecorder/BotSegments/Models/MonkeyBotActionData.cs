using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.ActionManager;
using RegressionGames.GenericBots;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [Serializable]
    [JsonConverter(typeof(MonkeyBotActionDataJsonConverter))]
    public class MonkeyBotActionData : IBotActionData
    {
        [NonSerialized] 
        public static readonly BotActionType Type = BotActionType.MonkeyBot;
        
        public int apiVersion = SdkApiVersion.VERSION_6;
        public float actionInterval = 0.05f;
        public RGActionManagerSettings actionSettings;

        [NonSerialized]
        private bool isStopped;

        [NonSerialized] 
        private RGMonkeyBotLogic monkey;
        
        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            var controller = UnityEngine.Object.FindObjectOfType<ReplayDataPlaybackController>();
            RGActionManager.StartSession(controller, actionSettings);
            monkey = new RGMonkeyBotLogic();
            monkey.ActionInterval = actionInterval;
        }

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            if (!isStopped)
            {
                bool didAnyAction = monkey.Update();
                error = null;
                return didAnyAction;
            }

            error = null;
            return false;
        }

        public void StopAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            RGActionManager.StopSession();
            isStopped = true;
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
            isStopped = false;
            monkey = null;
        }

        public bool IsCompleted()
        {
            return isStopped;
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
