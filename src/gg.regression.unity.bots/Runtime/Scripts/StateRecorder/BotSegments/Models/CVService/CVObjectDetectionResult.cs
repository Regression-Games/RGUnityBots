using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models.CVService
{
    public class CVObjectDetectionResult
    {
        /// <summary>
        /// The resolution of the image in which the object was detected.
        /// </summary>
        public Vector2Int resolution;

        /// <summary>
        /// The bounding box of the detected object within the image.
        /// </summary>
        public RectInt rect;

        /// <summary>
        /// The segment index associated with this detection result.
        /// </summary>
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
