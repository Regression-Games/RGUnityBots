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
        [CanBeNull] public CVImageBinaryData queryImage;

        /// <summary>
        /// Optional text query for object detection.
        /// </summary>
        [CanBeNull] public string queryText;

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
                                        string? queryText,
                                        CVImageBinaryData? queryImage,
                                        RectInt? withinRect,
                                        int index)
        {
            this.screenshot = screenshot;
            this.queryText = queryText;
            this.queryImage = queryImage;
            this.withinRect = withinRect;
            this.index = index;

            // !!! TODO(REG-1915) Move this check to where the json is parsed.
            if (queryText != null && queryImage != null)
            {
                RGDebug.LogError("Both queryText and queryImage are provided. Only one should be used.");
                return;
            }
            else if (queryText == null && queryImage == null)
            {
                RGDebug.LogError("Neither queryText nor queryImage is provided. One should be specified.");
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
            
            if (queryText != null)
            {
                stringBuilder.Append(",\"queryText\":");
                StringJsonConverter.WriteToStringBuilder(stringBuilder, queryText);
            }

            if (queryImage != null)
            {
                stringBuilder.Append(",\"imageToMatch\":");
                queryImage.WriteToStringBuilder(stringBuilder);
            }
            stringBuilder.Append(",\"withinRect\":");
            RectIntJsonConverter.WriteToStringBuilderNullable(stringBuilder, withinRect);
            stringBuilder.Append(",\"index\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, index);
            stringBuilder.Append("}");
        }
    }


}
