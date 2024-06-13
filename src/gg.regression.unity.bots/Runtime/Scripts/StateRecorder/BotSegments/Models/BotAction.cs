using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [JsonConverter(typeof(BotActionJsonConverter))]
    [Serializable]
    public class BotAction
    {
        // api version for this top level schema, update if we add/remove/change fields here
        public int apiVersion = BotSegment.SDK_API_VERSION_1;

        public BotActionType type;
        public IBotActionData data;
        public bool? IsCompleted => data.IsCompleted(); // returns null if this action runs until the keyframecriteria are met

        public int EffectiveApiVersion => Math.Max(apiVersion, data?.EffectiveApiVersion() ?? 0);

        // Called before the first call to ProcessAction to allow data setup by the action code
        public void StartAction(int segmentNumber, Dictionary<int, TransformStatus> currentUITransforms, Dictionary<int, TransformStatus> currentGameObjectTransforms)
        {
            data.StartAction(segmentNumber, currentUITransforms, currentGameObjectTransforms);
        }

        // called at least once per frame
        // bool firstCall tracks if this is the first call for this botaction
        public void ProcessAction(int segmentNumber, Dictionary<int, TransformStatus> currentUITransforms, Dictionary<int, TransformStatus> currentGameObjectTransforms)
        {
            data.ProcessAction(segmentNumber, currentUITransforms, currentGameObjectTransforms);
        }

        public void ReplayReset()
        {
            data.ReplayReset();
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, type.ToString());
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"data\":");
            data.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }
    }
}
