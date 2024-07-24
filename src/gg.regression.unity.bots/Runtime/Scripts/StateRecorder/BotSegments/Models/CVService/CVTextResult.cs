using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models.CVSerice
{
    public class CVTextResult
    {
        public string text;
        public Vector2Int resolution;
        public RectInt rect;

        public string ToString()
        {
            StringBuilder sb = new(1000);
            sb.Append("{\ntext\":");
            StringJsonConverter.WriteToStringBuilder(sb, text);
            sb.Append(",\resolution\":");
            VectorIntJsonConverter.WriteToStringBuilder(sb, resolution);
            sb.Append(",\"rect\":");
            RectIntJsonConverter.WriteToStringBuilder(sb, rect);
            sb.Append("}");

            return sb.ToString();
        }
    }
}
