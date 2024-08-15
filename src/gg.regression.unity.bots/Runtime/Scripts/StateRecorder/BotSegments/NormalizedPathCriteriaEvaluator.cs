using System.Collections.Generic;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder.BotSegments
{
    public static class NormalizedPathCriteriaEvaluator
    {

        // Track counts from the last keyframe completion and use that as the 'prior' data
        // Returns a list of non-matched entries
        public static List<string> Matched(int segmentNumber, List<KeyFrameCriteria> criteriaList, List<Dictionary<long, PathBasedDeltaCount>> deltaCounts)
        {

            var resultList = new List<string>();
            foreach (var criteria in criteriaList)
            {
                string matched = null; // null == matched, error message if not matched
                if (!(criteria.transient && criteria.Replay_TransientMatched))
                {
                    var criteriaPathData = criteria.data as PathKeyFrameCriteriaData;
                    var normalizedPath = criteriaPathData.path;
                    var pathHash = normalizedPath.GetHashCode();
                    // see if it is in the transforms path
                    var deltasListCount = deltaCounts.Count;
                    PathBasedDeltaCount objectCounts = null;
                    for (var i = 0; objectCounts == null && i < deltasListCount; i++)
                    {
                        var deltas = deltaCounts[i];
                        deltas.TryGetValue(pathHash, out objectCounts);
                    }
                    if (objectCounts != null)
                    {
                        // compare counts for match
                        switch (criteriaPathData.countRule)
                        {
                            case CountRule.Zero:
                                if (objectCounts.count != 0)
                                {
                                    matched = $"NormalizedPath (UI) - {normalizedPath} - CountRule.Zero - actual: {objectCounts.count}";
                                }
                                break;
                            case CountRule.NonZero:
                                if (objectCounts.count <= 0)
                                {
                                    matched = $"NormalizedPath (UI) - {normalizedPath} - CountRule.NonZero - actual: {objectCounts.count}";
                                }
                                break;
                            case CountRule.GreaterThanEqual:
                                if (objectCounts.count < criteriaPathData.count)
                                {
                                    matched = $"NormalizedPath (UI) - {normalizedPath} - CountRule.GreaterThanEqual - actual: {objectCounts.count} , expected: {criteriaPathData.count}";
                                }
                                break;
                            case CountRule.LessThanEqual:
                                if (objectCounts.count > criteriaPathData.count)
                                {
                                    matched = $"NormalizedPath (UI) - {normalizedPath} - CountRule.LessThanEqual - actual: {objectCounts.count} , expected: {criteriaPathData.count}";
                                }
                                break;
                        }

                        // then evaluate added / removed data
                        if (matched == null && objectCounts.addedCount < criteriaPathData.addedCount)
                        {
                            matched = $"NormalizedPath (UI) - {normalizedPath} - addedCount - actual: {objectCounts.addedCount} , expected: {criteriaPathData.addedCount}";
                        }

                        if (matched == null && objectCounts.removedCount < criteriaPathData.removedCount)
                        {
                            matched = $"NormalizedPath (UI) - {normalizedPath} - removedCount - actual: {objectCounts.removedCount} , expected: {criteriaPathData.removedCount}";
                        }
                    }
                    else
                    {
                        // unless this was supposed to be zero or less than zero ... criteria not met
                        if (! (criteriaPathData.countRule == CountRule.Zero || (criteriaPathData.countRule == CountRule.LessThanEqual && criteriaPathData.count <= 0)))
                        {
                            matched = $"NormalizedPath (WorldSpace) - {normalizedPath} - missing object";
                        }
                    }

                }

                if (matched == null)
                {
                    criteria.Replay_TransientMatched = true;
                }
                resultList.Add(matched);
            }

            return resultList;
        }
    }
}
