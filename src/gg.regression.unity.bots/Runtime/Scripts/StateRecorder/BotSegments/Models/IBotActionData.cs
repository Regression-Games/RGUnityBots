using System.Text;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    public interface IBotActionData
    {
        public void WriteToStringBuilder(StringBuilder stringBuilder);

        public void ReplayReset();

        public bool IsCompleted();
    }
}
