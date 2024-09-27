using System.Collections.Generic;

namespace RegressionGames.StateRecorder.Models
{
    public class PathBasedDeltaCount
    {
        public PathBasedDeltaCount(int pathHash, string path)
        {
            this.pathHash = pathHash;
            this.path = path;
        }

        public readonly HashSet<long> ids = new();
        public readonly int pathHash;
        public readonly string path;

        /**
         * <summary>Tracks the 'visible'/'onCamera' count of this path</summary>
         */
        public int count;

        // if negative, this count went down CountRule.LessThanEqual; if positive, this count went up CountRule.GreaterThanEqual; if zero, this count didn't change CountRule.NonZero; if zero and count ==0, CountRule.Zero
        public int higherLowerCountTracker;

        public override string ToString()
        {
            // easier debugging
            return path + " | " + pathHash + " - c:" + count + ", hlc:" + higherLowerCountTracker + ", ids:["+string.Join(",",ids)+"]";
        }
    }
}
