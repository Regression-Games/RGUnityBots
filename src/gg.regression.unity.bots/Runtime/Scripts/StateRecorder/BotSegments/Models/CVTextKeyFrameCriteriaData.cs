using System;
using System.Text;
using System.Threading;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [Serializable]
    public class CVTextKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        // update this if this schema changes
        public int apiVersion = SdkApiVersion.VERSION_8;


        public string text;
        public TextMatchingRule textMatchingRule = TextMatchingRule.Matches;
        public TextCaseRule textCaseRule = TextCaseRule.Matches;

        public Vector2Int resolution;
        public RectInt? withinRect;

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
            stringBuilder.Append(",\"resolution\":");
            VectorIntJsonConverter.WriteToStringBuilder(stringBuilder, resolution);
            stringBuilder.Append(",\"withinRect\":");
            RectIntJsonConverter.WriteToStringBuilderNullable(stringBuilder, withinRect);
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
