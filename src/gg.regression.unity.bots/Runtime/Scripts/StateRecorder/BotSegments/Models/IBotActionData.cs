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
         * Handles resuming the paused action if un-paused from the UI; this is most useful for input playback
         */
        public void UnPauseAction(int segmentNumber)
        {
            // no-op default
        }

        /**
          * Handle pausing the action if paused from the UI; this is most useful for input playback
          */
        public void PauseAction(int segmentNumber)
        {
            // no-op default
        }

        /**
         * Called at least once per frame
         * returns true if an action was performed
         * Returns null or a error string
         */
        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error);

        /**
         * Called when a user or ui stops a playback.
         * Even for segments with action sequences, they should stop as soon as possible.
         */
        public void AbortAction(int segmentNumber);

        /**
         * Called when a segment completes to stop any outstanding actions or mark the action as should stop.
         * For segments with action sequences, they should still finish processing all their actions before stopping.
         * By default this calls AbortAction(segmentNumber)
         */
        public void StopAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            AbortAction(segmentNumber);
        }

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
