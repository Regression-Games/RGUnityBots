using System;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [JsonConverter(typeof(KeyFrameCriteriaJsonConverter))]
    [Serializable]
    public class KeyFrameCriteria
    {
        // api version of this top level schema, update if we add/change fields
        public int apiVersion = BotSegment.SDK_API_VERSION_1;

        public KeyFrameCriteriaType type;
        public bool transient;
        public IKeyFrameCriteriaData data;

        public int EffectiveApiVersion => Math.Max(apiVersion, data.EffectiveApiVersion());


        // Replay only - used to track if transient has ever matched during replay
        [NonSerialized]
        public bool Replay_TransientMatched;

        public void ReplayReset()
        {
            Replay_TransientMatched = false;
            if (data is OrKeyFrameCriteriaData okc)
            {
                foreach (var keyFrameCriteria in okc.criteriaList)
                {
                    keyFrameCriteria.ReplayReset();
                }
            }
            else if (data is AndKeyFrameCriteriaData akc)
            {
                foreach (var keyFrameCriteria in akc.criteriaList)
                {
                    keyFrameCriteria.ReplayReset();
                }
            }
        }

        public override string ToString()
        {
            return "{type:" + type + ",transient:" + transient + ",data:" + data.ToString() + "}";
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, type.ToString());
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"transient\":");
            stringBuilder.Append(transient ? "true" : "false");
            stringBuilder.Append(",\"data\":");
            data.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }
    }
}
