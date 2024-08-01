using System;
using System.Text;
using System.Threading;
using RegressionGames.StateRecorder.JsonConverters;
using StateRecorder.BotSegments.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [Serializable]
    public class CVTextKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        // update this if this schema changes
        public int apiVersion = SdkApiVersion.VERSION_9;

        public string text;
        public TextMatchingRule textMatchingRule = TextMatchingRule.Matches;
        public TextCaseRule textCaseRule = TextCaseRule.Matches;

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
            withinRect.WriteToStringBuilder(stringBuilder);
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
