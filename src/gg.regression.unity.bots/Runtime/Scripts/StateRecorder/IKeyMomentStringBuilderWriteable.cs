using System.Text;
using System.Threading;

namespace RegressionGames.StateRecorder
{
    public interface IKeyMomentStringBuilderWriteable
    {

        static readonly ThreadLocal<StringBuilder> StringBuilder = new (() => new());

        public void WriteKeyMomentToStringBuilder(StringBuilder stringBuilder);

        public string ToKeyMomentJsonString()
        {
            var sb = StringBuilder.Value;
            sb.Clear();
            WriteKeyMomentToStringBuilder(sb);
            return sb.ToString();
        }
    }

}
