using System.Text;
using System.Threading;

namespace RegressionGames.StateRecorder
{
    public interface IStringBuilderWriteable
    {

        static readonly ThreadLocal<StringBuilder> StringBuilder = new (() => new());

        public void WriteToStringBuilder(StringBuilder stringBuilder);

        public string ToJsonString()
        {
            var sb = StringBuilder.Value;
            sb.Clear();
            WriteToStringBuilder(sb);
            return sb.ToString();
        }
    }

}
