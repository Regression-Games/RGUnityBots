using System.Text;
using System.Threading;

namespace RegressionGames.StateRecorder.BotSegments.Models.CVService
{
    public class CVTextCriteriaRequest
    {
        public CVImageBinaryData screenshot;

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
            stringBuilder.Append("}");
        }
    }


}
