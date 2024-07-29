using System.Text;
using System.Threading;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models.CVSerice
{
    public class CVImageCriteriaRequest
    {
        public CVImageBinaryData screenshot;
        public CVImageEncodedData ImageRequestToMatch;
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
            stringBuilder.Append("{\"screenshot\":");
            screenshot.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append(",\"imageToMatch\":");
            ImageRequestToMatch.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append(",\"index\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, index);
            stringBuilder.Append("}");
        }
    }


}
