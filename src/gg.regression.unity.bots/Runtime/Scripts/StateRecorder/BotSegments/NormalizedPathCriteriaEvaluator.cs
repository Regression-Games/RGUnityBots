using System.Collections.Generic;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder.BotSegments
{
    public static class NormalizedPathCriteriaEvaluator
    {
        // Track counts from the last keyframe completion and use that as the 'prior' data
        public static List<bool> Matched(List<KeyFrameCriteria> criteriaList, Dictionary<int, TransformStatus> priorUIStatus, Dictionary<int, TransformStatus> priorGameObjectStatus, Dictionary<int, TransformStatus> uiTransforms, Dictionary<int, TransformStatus> gameObjectTransforms)
        {
            //Compute the frame deltas (Use InGameObjectFinder).. then evaluate
            var deltaUI = InGameObjectFinder.GetInstance().ComputeNormalizedPathBasedDeltaCounts(priorUIStatus, uiTransforms, out var _, out var _);
            var deltaGameObjects = InGameObjectFinder.GetInstance().ComputeNormalizedPathBasedDeltaCounts(priorGameObjectStatus, gameObjectTransforms, out var _, out var _);
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

                        if (matched)
                        {
                            // then evaluate renderer count rules
                            // compare counts for match
                            switch (pathData.rendererCountRule)
                            {
                                case CountRule.Zero:
                                    if (uiObjectCounts.rendererCount != 0)
                                    {
                                        matched = false;
                                    }
                                    break;
                                case CountRule.NonZero:
                                    if (uiObjectCounts.rendererCount <= 0)
                                    {
                                        matched = false;
                                    }
                                    break;
                                case CountRule.GreaterThanEqual:
                                    if (uiObjectCounts.rendererCount < pathData.rendererCount)
                                    {
                                        matched = false;
                                    }
                                    break;
                                case CountRule.LessThanEqual:
                                    if (uiObjectCounts.rendererCount > pathData.rendererCount)
                                    {
                                        matched = false;
                                    }
                                    break;
                            }
                        }

                        // then evaluate added / removed data
                        if (matched && uiObjectCounts.addedCount != pathData.addedCount)
                        {
                            matched = false;
                        }

                        if (matched && uiObjectCounts.removedCount != pathData.removedCount)
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

                        if (matched)
                        {
                            // then evaluate renderer count rules
                            // compare counts for match
                            switch (pathData.rendererCountRule)
                            {
                                case CountRule.Zero:
                                    if (gameObjectCounts.rendererCount != 0)
                                    {
                                        matched = false;
                                    }
                                    break;
                                case CountRule.NonZero:
                                    if (gameObjectCounts.rendererCount <= 0)
                                    {
                                        matched = false;
                                    }
                                    break;
                                case CountRule.GreaterThanEqual:
                                    if (gameObjectCounts.rendererCount < pathData.rendererCount)
                                    {
                                        matched = false;
                                    }
                                    break;
                                case CountRule.LessThanEqual:
                                    if (gameObjectCounts.rendererCount > pathData.rendererCount)
                                    {
                                        matched = false;
                                    }
                                    break;
                            }
                        }

                        // then evaluate added / removed data
                        if (matched && gameObjectCounts.addedCount != pathData.addedCount)
                        {
                            matched = false;
                        }

                        if (matched && gameObjectCounts.removedCount != pathData.removedCount)
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
