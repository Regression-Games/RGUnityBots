using System;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [Serializable]
    public class PathKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        // update this if this schema changes
        public int apiVersion = SdkApiVersion.VERSION_22;

        public string path;
        public int count;
        public CountRule countRule;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"path\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, path);
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"count\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, count);
            stringBuilder.Append(",\"countRule\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, countRule.ToString());
            stringBuilder.Append("}");
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(1000);
            WriteToStringBuilder(sb);
            return sb.ToString();
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
