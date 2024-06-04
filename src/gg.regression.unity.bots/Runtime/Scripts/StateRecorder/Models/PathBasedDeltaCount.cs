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

        public List<int> ids = new();
        public int pathHash;
        public string path;

        public int count;
        public int addedCount;

        public int removedCount;

        // if negative, this count went down CountRule.LessThanEqual; if positive, this count went up CountRule.GreaterThanEqual; if zero, this count didn't change CountRule.NonZero; if zero and count ==0, CountRule.Zero
        public int higherLowerCountTracker;

        public int rendererCount;

        public int higherLowerRendererCountTracker;
    }
}
