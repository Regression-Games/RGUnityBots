using System;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;

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
        public bool IsCompleted => data.IsCompleted();

        public int EffectiveApiVersion => Math.Max(apiVersion, data?.EffectiveApiVersion() ?? 0);

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
