using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models.CVSerice
{
    public class CVImageResult
    {
        public Vector2Int resolution;
        public RectInt rect;
        public int index;

        public override string ToString()
        {
            StringBuilder sb = new(1000);
            sb.Append("{\"resolution\":");
            VectorIntJsonConverter.WriteToStringBuilder(sb, resolution);
            sb.Append(",\"rect\":");
            RectIntJsonConverter.WriteToStringBuilder(sb, rect);
            sb.Append(",\"index\":");
            IntJsonConverter.WriteToStringBuilder(sb, index);
            sb.Append("}");

            return sb.ToString();
        }
    }
}
