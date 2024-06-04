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
        public KeyFrameCriteriaType type;
        public bool transient;
        public IKeyFrameCriteriaData data;

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
            stringBuilder.Append("{\n\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, type.ToString());
            stringBuilder.Append(",\n\"transient\":");
            stringBuilder.Append(transient ? "true" : "false");
            stringBuilder.Append(",\n\"data\":");
            data.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("\n}");
        }
    }
}
