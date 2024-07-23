using System.Text;
using System.Threading;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    public class CVTextCriteriaRequest
    {
        public CVImageRequestData screenshot;

        private static ThreadLocal<StringBuilder> _stringBuilder = new (() => new(1000));
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
