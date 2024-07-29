using System;
using System.Text;
using System.Threading;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.BotSegments.Models.CVSerice;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [Serializable]
    public class CVImageKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        // update this if this schema changes
        public int apiVersion = SdkApiVersion.VERSION_9;

        public Vector2Int resolution;

        /**
         * base64 encoded byte[] of jpg image data , NOT the raw pixel data, the full jpg file bytes
         */
        public CVImageEncodedData imageData;
        public RectInt? withinRect;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"imageData\":");
            imageData.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
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
