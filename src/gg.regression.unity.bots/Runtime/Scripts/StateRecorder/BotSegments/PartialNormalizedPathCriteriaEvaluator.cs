using System.Collections.Generic;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments
{
    public static class PartialNormalizedPathCriteriaEvaluator
    {
        // Track counts from the last keyframe completion and use that as the 'prior' data
        public static List<string> Matched(int segmentNumber, List<KeyFrameCriteria> criteriaList, List<Dictionary<long, PathBasedDeltaCount>> deltaCounts)
        {
            var resultList = new List<string>();
            foreach (var criteria in criteriaList)
            {
                string matched = null; // null == matched, error message if not matched
                if (!(criteria.transient && criteria.Replay_TransientMatched))
                {
                    var criteriaPathData = criteria.data as PathKeyFrameCriteriaData;
                    var partialNormalizedPath = criteriaPathData.path;
                    // see if it is in the transforms path
                    var deltasListCount = deltaCounts.Count;
                    PathBasedDeltaCount objectCounts = null;
                    for (var i = 0; objectCounts == null && i < deltasListCount; i++)
                    {
                        var deltas = deltaCounts[i];
                        // Partial matching performs awful, we may need to think whether the criteria list will be smaller or the paths will be smaller to organize these loops in the least poorly performing way
                        foreach (var pathBasedDeltaCount in deltas)
                        {
                            if (pathBasedDeltaCount.Value.path.Contains(partialNormalizedPath))
                            {
                                objectCounts = pathBasedDeltaCount.Value;
                                break;
                            }
                        }
                    }
                    if (objectCounts != null)
                    {
                        // compare counts for match
                        switch (criteriaPathData.countRule)
                        {
                            case CountRule.Zero:
                                if (objectCounts.count != 0)
                                {
                                    matched = $"PartialNormalizedPath (UI) - {partialNormalizedPath} - CountRule.Zero - actual: {objectCounts.count}";
                                }
                                break;
                            case CountRule.NonZero:
                                if (objectCounts.count <= 0)
                                {
                                    matched = $"PartialNormalizedPath (UI) - {partialNormalizedPath} - CountRule.NonZero - actual: {objectCounts.count}";
                                }
                                break;
                            case CountRule.GreaterThanEqual:
                                if (objectCounts.count < criteriaPathData.count)
                                {
                                    matched = $"PartialNormalizedPath (UI) - {partialNormalizedPath} - CountRule.GreaterThanEqual - actual: {objectCounts.count} , expected: {criteriaPathData.count}";
                                }
                                break;
                            case CountRule.LessThanEqual:
                                if (objectCounts.count > criteriaPathData.count)
                                {
                                    matched = $"PartialNormalizedPath (UI) - {partialNormalizedPath} - CountRule.LessThanEqual - actual: {objectCounts.count} , expected: {criteriaPathData.count}";
                                }
                                break;
                        }

                        // then evaluate added / removed data
                        if (matched == null && objectCounts.addedCount < criteriaPathData.addedCount)
                        {
                            matched = $"PartialNormalizedPath (UI) - {partialNormalizedPath} - addedCount - actual: {objectCounts.addedCount} , expected: {criteriaPathData.addedCount}";
                        }

                        if (matched == null && objectCounts.removedCount < criteriaPathData.removedCount)
                        {
                            matched = $"PartialNormalizedPath (UI) - {partialNormalizedPath} - removedCount - actual: {objectCounts.removedCount} , expected: {criteriaPathData.removedCount}";
                        }
                    }
                    else
                    {
                        // else - criteria not met - false
                        matched = $"PartialNormalizedPath (WorldSpace) - {partialNormalizedPath} - missing object";
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
