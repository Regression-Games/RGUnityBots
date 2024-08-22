using System;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [Serializable]
    public class CVObjectDetectionKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        // update this if this schema changes
        public int apiVersion = SdkApiVersion.VERSION_15;

        public string text;
        /**
       * base64 encoded byte[] of jpg image data , NOT the raw pixel data, the full jpg file bytes
       */
        // TODO(REG-1915) Add supporting image queries.
        // public string imageData; 
        public TextMatchingRule textMatchingRule = TextMatchingRule.Matches;
        public TextCaseRule textCaseRule = TextCaseRule.Matches;

        [CanBeNull]
        public CVWithinRect withinRect;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"text\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, text);
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"textMatchingRule\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, textMatchingRule.ToString());
            stringBuilder.Append(",\"textCaseRule\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, textCaseRule.ToString());
            stringBuilder.Append(",\"withinRect\":");
            CVWithinRectJsonConverter.WriteToStringBuilderNullable(stringBuilder, withinRect);
            stringBuilder.Append("}");
        }

        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(1000));

        public override string ToString()
        {
            WriteToStringBuilder(_stringBuilder.Value);
            return _stringBuilder.Value.ToString();
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
