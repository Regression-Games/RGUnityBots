using System.Collections.Generic;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder
{
    public class RecordedGameObjectStatePathEqualityComparer : IEqualityComparer<TransformStatus>
    {
        public bool Equals(TransformStatus x, TransformStatus y)
        {
            if (x?.worldSpaceBounds != null || y?.worldSpaceBounds != null)
            {
                // for world space objects, we don't want to unique-ify based on path
                return false;
            }

            return x?.Path == y?.Path;
        }

        public int GetHashCode(TransformStatus obj)
        {
            return obj.Path.GetHashCode();
        }
    }
}
