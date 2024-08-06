using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models.CVService
{
    public class CVTextResult
    {
        public string text;
        public Vector2Int resolution;
        public RectInt rect;

        public override string ToString()
        {
            StringBuilder sb = new(1000);
            sb.Append("{\"text\":");
            StringJsonConverter.WriteToStringBuilder(sb, text);
            sb.Append(",\"resolution\":");
            VectorIntJsonConverter.WriteToStringBuilder(sb, resolution);
            sb.Append(",\"rect\":");
            RectIntJsonConverter.WriteToStringBuilder(sb, rect);
            sb.Append("}");

            return sb.ToString();
        }
    }
}
