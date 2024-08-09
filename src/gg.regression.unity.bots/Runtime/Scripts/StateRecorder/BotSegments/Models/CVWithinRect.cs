using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [JsonConverter(typeof(CVWithinRectJsonConverter))]
    public class CVWithinRect
    {

        public int apiVersion = SdkApiVersion.VERSION_9;

        public Vector2Int screenSize;
        public RectInt rect;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"screenSize\":");
            VectorIntJsonConverter.WriteToStringBuilder(stringBuilder, screenSize);
            stringBuilder.Append(",\"rect\":");
            RectIntJsonConverter.WriteToStringBuilderNullable(stringBuilder, rect);
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilderNullable(stringBuilder, apiVersion);
            stringBuilder.Append("}");
        }

        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(1000));

        public override string ToString()
        {
            WriteToStringBuilder(_stringBuilder.Value);
            return _stringBuilder.Value.ToString();
        }
    }
}
