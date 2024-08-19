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

        
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new (() => new(1000));
        public string ToJsonString()
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value);
            return _stringBuilder.Value.ToString();
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {

            bool textQuery = queryText != null;
            bool imageQuery = queryImage != null;

            if (textQuery && imageQuery)
            {
                RGDebug.LogError("You need to use either textQuery or imageQuery. You can not have both.");
            }
            
            if (!textQuery && !imageQuery)
            {
                RGDebug.LogError("You need to use either textQuery or imageQuery. You need to have at least one.");
            }
            
            stringBuilder.Append("{\"screenshot\":");
            screenshot.WriteToStringBuilder(stringBuilder);
            
            if (textQuery)
            {
                stringBuilder.Append(",\"queryText\":");
                StringJsonConverter.WriteToStringBuilder(stringBuilder, queryText);
            }

            if (imageQuery)
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
