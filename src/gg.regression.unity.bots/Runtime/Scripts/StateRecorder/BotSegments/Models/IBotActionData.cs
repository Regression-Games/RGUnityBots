using System.Collections.Generic;
using System.Text;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    public interface IBotActionData
    {

        /**
         * Called before the first call to ProcessAction to allow data setup by the action
         */
        public void StartAction(int segmentNumber, Dictionary<int, TransformStatus> currentUITransforms, Dictionary<int, TransformStatus> currentGameObjectTransforms);

        /**
         * Called at least once per frame
         */
        public void ProcessAction(int segmentNumber, Dictionary<int, TransformStatus> currentUITransforms, Dictionary<int, TransformStatus> currentGameObjectTransforms);

        public void WriteToStringBuilder(StringBuilder stringBuilder);

        public void ReplayReset();

        /**
         * Returns null if the action runs until the keyframecriteria match
         */
        public bool? IsCompleted();

        public int EffectiveApiVersion();
    }
}
