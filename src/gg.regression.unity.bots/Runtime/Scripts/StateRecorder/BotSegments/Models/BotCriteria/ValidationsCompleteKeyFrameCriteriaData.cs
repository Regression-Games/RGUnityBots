using System;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models.BotCriteria
{
    [Serializable]
    public class ValidationsCompleteKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        public int apiVersion = SdkApiVersion.VERSION_11;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append("}");
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(100);
            WriteToStringBuilder(sb);
            return sb.ToString();
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}