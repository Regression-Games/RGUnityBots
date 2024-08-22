using System.Text;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models.CVService
{
    public class CVObjectDetectionRequest
    {
        public CVImageBinaryData screenshot;
        [CanBeNull] public CVImageBinaryData queryImage;
        [CanBeNull] public string queryText;
        /*
         * <summary>Rect relative to the screenshot resolution</summary>
        */
        public RectInt? withinRect;

        // track the index in this bot segment for correlation of the responses
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
