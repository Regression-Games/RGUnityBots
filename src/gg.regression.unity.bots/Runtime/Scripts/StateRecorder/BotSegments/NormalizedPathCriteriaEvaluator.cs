using System.Collections.Generic;
using RegressionGames.StateRecorder;
using StateRecorder.BotSegments.Models;
using StateRecorder.Models;

namespace StateRecorder.BotSegments
{
    public static class NormalizedPathCriteriaEvaluator
    {
        // Track counts from the last keyframe completion and use that as the 'prior' data
        public static List<bool> Matched(List<KeyFrameCriteria> criteriaList, Dictionary<int, TransformStatus> priorUIStatus, Dictionary<int, TransformStatus> priorGameObjectStatus, Dictionary<int, TransformStatus> uiTransforms, Dictionary<int, TransformStatus> gameObjectTransforms)
        {
            //Compute the frame deltas (Use InGameObjectFinder).. then evaluate
            var deltaUI = InGameObjectFinder.GetInstance().ComputeNormalizedPathBasedDeltaCounts(priorUIStatus, uiTransforms, out var hasUIDelta);
            var deltaGameObjects = InGameObjectFinder.GetInstance().ComputeNormalizedPathBasedDeltaCounts(priorGameObjectStatus, gameObjectTransforms, out var hasGameDelta);
            var resultList = new List<bool>();
            foreach (var criteria in criteriaList)
            {
                var matched = false;
                if (criteria.transient && criteria.Replay_TransientMatched)
                {
                    matched = true;
                }
                else
                {
                    var pathData = criteria.data as PathKeyFrameCriteriaData;
                    var normalizedPath = pathData.path;
                    var pathHash = normalizedPath.GetHashCode();
                    // see if it is in the UI path
                    if (deltaUI.TryGetValue(pathHash, out var uiObjectCounts))
                    {
                        // compare counts for match
                        switch (pathData.countRule)
                        {
                            case CountRule.Zero:
                                if (uiObjectCounts.count == 0)
                                {
                                    matched = true;
                                }
                                break;
                            case CountRule.NonZero:
                                if (uiObjectCounts.count > 0)
                                {
                                    matched = true;
                                }
                                break;
                            case CountRule.GreaterThanEqual:
                                if (uiObjectCounts.count >= pathData.count)
                                {
                                    matched = true;
                                }
                                break;
                            case CountRule.LessThanEqual:
                                if (uiObjectCounts.count <= pathData.count)
                                {
                                    matched = true;
                                }
                                break;
                        }
                        // then evaluate added / removed data
                        if (uiObjectCounts.addedCount != pathData.addedCount)
                        {
                            matched = false;
                        }

                        if (uiObjectCounts.removedCount != pathData.removedCount)
                        {
                            matched = false;
                        }
                    }
                    else if (deltaGameObjects.TryGetValue(pathHash, out var gameObjectCounts))
                    {
                        // compare counts for match
                        switch (pathData.countRule)
                        {
                            case CountRule.Zero:
                                if (gameObjectCounts.count == 0)
                                {
                                    matched = true;
                                }
                                break;
                            case CountRule.NonZero:
                                if (gameObjectCounts.count > 0)
                                {
                                    matched = true;
                                }
                                break;
                            case CountRule.GreaterThanEqual:
                                if (gameObjectCounts.count >= pathData.count)
                                {
                                    matched = true;
                                }
                                break;
                            case CountRule.LessThanEqual:
                                if (gameObjectCounts.count <= pathData.count)
                                {
                                    matched = true;
                                }
                                break;
                        }
                        // then evaluate added / removed data
                        if (gameObjectCounts.addedCount != pathData.addedCount)
                        {
                            matched = false;
                        }

                        if (gameObjectCounts.removedCount != pathData.removedCount)
                        {
                            matched = false;
                        }
                    }
                    // else - criteria not met - false
                }

                if (matched)
                {
                    criteria.Replay_TransientMatched = true;
                }
                resultList.Add(matched);
            }

            return resultList;
        }
    }
}
