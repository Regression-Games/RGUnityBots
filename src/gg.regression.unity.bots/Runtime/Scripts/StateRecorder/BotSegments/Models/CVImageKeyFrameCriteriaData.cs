using System;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;
using StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [Serializable]
    public class CVImageKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        // update this if this schema changes
        public int apiVersion = SdkApiVersion.VERSION_9;

        /**
         * base64 encoded byte[] of jpg image data , NOT the raw pixel data, the full jpg file bytes
         */
        public string imageData;

        [CanBeNull]
        public CVWithinRect withinRect;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"imageData\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, imageData);
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
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
