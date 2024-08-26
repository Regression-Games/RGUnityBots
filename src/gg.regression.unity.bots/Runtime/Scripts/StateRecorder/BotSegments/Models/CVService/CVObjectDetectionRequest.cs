using System.Text;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models.CVService
{
    public class CVObjectDetectionRequest
    {
        /// <summary>
        /// The screenshot image data to be analyzed.
        /// </summary>
        public CVImageBinaryData screenshot;

        /// <summary>
        /// Optional image data to be used as a query for object detection.
        /// </summary>
        [CanBeNull] public CVImageBinaryData imageQuery;

        /// <summary>
        /// Optional text query for object detection.
        /// </summary>
        [CanBeNull] public string textQuery;

        /// <summary>
        /// Optional rectangle defining a region of interest within the screenshot.
        /// The coordinates are relative to the screenshot.
        /// </summary>
        public RectInt? withinRect;

        /// <summary>
        /// Index to track requests and results for each criteria within a segment.
        /// </summary>
        public int index;

        public CVObjectDetectionRequest(CVImageBinaryData screenshot,
                                        [CanBeNull] string textQuery,
                                        [CanBeNull] CVImageBinaryData imageQuery,
                                        RectInt? withinRect,
                                        int index)
        {
            this.screenshot = screenshot;
            this.textQuery = textQuery;
            this.imageQuery = imageQuery;
            this.withinRect = withinRect;
            this.index = index;

            if (textQuery != null && imageQuery != null)
            {
                RGDebug.LogError("Both textQuery and imageQuery are provided. Only one should be used.");
                return;
            }
            else if (textQuery == null && imageQuery == null)
            {
                RGDebug.LogError("Neither textQuery nor imageQuery is provided. One should be specified.");
                return;
            }
        }
        
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new (() => new(1000));
        public string ToJsonString()
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value);
            return _stringBuilder.Value.ToString();
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {

            stringBuilder.Append("{\"screenshot\":");
            screenshot.WriteToStringBuilder(stringBuilder);
            
            if (textQuery != null)
            {
                stringBuilder.Append(",\"textQuery\":");
                StringJsonConverter.WriteToStringBuilder(stringBuilder, textQuery);
            }

            if (imageQuery != null)
            {
                stringBuilder.Append(",\"imageToMatch\":");
                imageQuery.WriteToStringBuilder(stringBuilder);
            }
            stringBuilder.Append(",\"withinRect\":");
            RectIntJsonConverter.WriteToStringBuilderNullable(stringBuilder, withinRect);
            stringBuilder.Append(",\"index\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, index);
            stringBuilder.Append("}");
        }
    }


}
