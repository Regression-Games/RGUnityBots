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
        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities);

        /**
         * Called at least once per frame
         * returns true if an action was performed
         * Returns null or an error message string
         */
        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error);

        /**
         * Called when a segment completes to stop any outstanding actions or mark the action as should stop.
         * For segments with action sequences, they should still finish processing all their actions before stopping.
         */
        public void StopAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities);

        public void WriteToStringBuilder(StringBuilder stringBuilder);

        public void ReplayReset();

        public bool IsCompleted();

        public int EffectiveApiVersion();

        // Allows the impl to draw debug elements on the screen
        public void OnGUI(Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            // default to no-op impl
        }
    }
}
