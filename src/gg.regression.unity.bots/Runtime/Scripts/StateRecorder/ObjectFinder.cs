using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.BotCriteria;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder
{
    public abstract class ObjectFinder : MonoBehaviour
    {
        public abstract (Dictionary<long, RecordedGameObjectState>, Dictionary<long, RecordedGameObjectState>) GetStateForCurrentFrame();

        public virtual List<KeyFrameCriteria> GetKeyFrameCriteriaForCurrentFrame(out bool hasDeltas)
        {
            var entities = GetObjectStatusForCurrentFrame();

            var deltas = ComputeNormalizedPathBasedDeltaCounts(entities.Item1, entities.Item2, out hasDeltas);

            return deltas.Values
                .Select(a => new KeyFrameCriteria()
                {
                    type = KeyFrameCriteriaType.NormalizedPath,
                    transient = true,
                    data = new PathKeyFrameCriteriaData()
                    {
                        path = a.path,
                        count = a.count,
                        countRule = a.higherLowerCountTracker == 0 ? (a.count == 0 ? CountRule.Zero : CountRule.NonZero) : (a.higherLowerCountTracker > 0 ? CountRule.GreaterThanEqual : CountRule.LessThanEqual)
                    }
                })
                .ToList();
        }
        public abstract (Dictionary<long, ObjectStatus>, Dictionary<long, ObjectStatus>) GetObjectStatusForCurrentFrame();
        public virtual Dictionary<long, PathBasedDeltaCount> ComputeNormalizedPathBasedDeltaCounts(Dictionary<long, ObjectStatus> priorStatusList, Dictionary<long, ObjectStatus> currentStatusList, out bool hasDelta)
        {
            hasDelta = false;
            var result = new Dictionary<long, PathBasedDeltaCount>(); // keyed by path hash
            /*
             * go through the new state and add up the totals
             * - track the ids for each path
             * - compute spawns vs old state
             *
             * go through the old state
             *  - track paths that have had de-spawns
             */
            foreach (var currentEntry in currentStatusList.Values)
            {
                var pathHash = currentEntry.NormalizedPath.GetHashCode();
                if (!result.TryGetValue(pathHash, out var pathCountEntry))
                {
                    pathCountEntry = new PathBasedDeltaCount(pathHash, currentEntry.NormalizedPath);
                    result[pathHash] = pathCountEntry;
                }

                var onCameraNow = (currentEntry.screenSpaceBounds != null);

                // only update 'count' for things on screen, but added/removed count are updated always
                if (onCameraNow)
                {
                    pathCountEntry.count++;
                }

                // ids is used to track changes from tick to tick
                pathCountEntry.ids.Add(currentEntry.Id);

                // figure out newly visible objects
                if (!priorStatusList.TryGetValue(currentEntry.Id, out _))
                {
                    hasDelta = true;
                    pathCountEntry.higherLowerCountTracker++;
                }
            }

            // figure out no longer visible objects
            foreach( KeyValuePair<long, ObjectStatus> entry in priorStatusList)
            {
                var pathHash = entry.Value.NormalizedPath.GetHashCode();
                if (!result.TryGetValue(pathHash, out var pathCountEntry))
                {
                    // this object wasn't in our result.. add an entry to so we can track the change in visibility
                    pathCountEntry = new PathBasedDeltaCount(pathHash, entry.Value.NormalizedPath);
                    result[pathHash] = pathCountEntry;
                }

                if (!pathCountEntry.ids.Contains(entry.Key))
                {
                    hasDelta = true;
                    pathCountEntry.higherLowerCountTracker--;
                }
            }

            return result;
        }

        public abstract void Cleanup();
    }
}
