using System;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [Serializable]
    public class PathKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        public string path;
        public int removedCount;
        public int addedCount;
        public int count;
        public CountRule countRule;
        public int rendererCount;
        public CountRule rendererCountRule;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"path\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, path);
            stringBuilder.Append(",\"removedCount\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, removedCount);
            stringBuilder.Append(",\"addedCount\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, addedCount);
            stringBuilder.Append(",\"count\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, count);
            stringBuilder.Append(",\"countRule\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, countRule.ToString());
            stringBuilder.Append(",\"rendererCount\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, rendererCount);
            stringBuilder.Append(",\"rendererCountRule\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, rendererCountRule.ToString());
            stringBuilder.Append("}");
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(1000);
            WriteToStringBuilder(sb);
            return sb.ToString();
        }
    }
}
