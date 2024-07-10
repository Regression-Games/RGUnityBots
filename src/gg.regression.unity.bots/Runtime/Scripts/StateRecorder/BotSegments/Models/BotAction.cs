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
        public int apiVersion = SdkApiVersion.VERSION_1;

        public BotActionType type;
        public IBotActionData data;
        public bool? IsCompleted => data.IsCompleted(); // returns null if this action runs until the keyframecriteria are met

        public int EffectiveApiVersion => Math.Max(apiVersion, data?.EffectiveApiVersion() ?? 0);

        // Called before the first call to ProcessAction to allow data setup by the action code
        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            data.StartAction(segmentNumber, currentTransforms, currentEntities);
        }

        // called once per frame
        // returns true if an action was performed
        // out error will have an error string or null if no action performed or no error
        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            return data.ProcessAction(segmentNumber, currentTransforms, currentEntities, out error);
        }

        public void ReplayReset()
        {
            data.ReplayReset();
        }

        public void OnGUI(Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            data.OnGUI(currentTransforms, currentEntities);
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
