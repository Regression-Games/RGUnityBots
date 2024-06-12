using System.Text;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    public interface IBotActionData
    {
        /**
         * Called at least once per frame
         */
        public void ProcessAction();

        public void WriteToStringBuilder(StringBuilder stringBuilder);

        public void ReplayReset();

        /**
         * Returns null if the action runs until the keyframecriteria match
         */
        public bool? IsCompleted();

        public int EffectiveApiVersion();
    }
}
