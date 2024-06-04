using System.Text;

namespace StateRecorder.BotSegments.Models
{
    public interface IBotActionData
    {
        public void WriteToStringBuilder(StringBuilder stringBuilder);

        public void ReplayReset();

        public bool IsCompleted();
    }
}
