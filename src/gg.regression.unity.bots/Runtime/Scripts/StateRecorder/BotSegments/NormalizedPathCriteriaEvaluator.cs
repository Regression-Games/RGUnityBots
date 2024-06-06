using System.Collections.Generic;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments
{
    public static class NormalizedPathCriteriaEvaluator
    {

        private static Dictionary<int, PathBasedDeltaCount> _deltaUI;
        private static Dictionary<int, PathBasedDeltaCount> _deltaGameObjects;

        private static int _lastFrameEvaluated = -1;

        private static void UpdateDeltaCounts(Dictionary<int, TransformStatus> priorUIStatus, Dictionary<int, TransformStatus> priorGameObjectStatus, Dictionary<int, TransformStatus> uiTransforms, Dictionary<int, TransformStatus> gameObjectTransforms)
        {
            var currentFrameCount = Time.frameCount;
            if (_lastFrameEvaluated != currentFrameCount)
            {
                _lastFrameEvaluated = currentFrameCount;
                //Compute the frame deltas (Use InGameObjectFinder)... but only once per frame
                _deltaUI = InGameObjectFinder.GetInstance().ComputeNormalizedPathBasedDeltaCounts(priorUIStatus, uiTransforms, out _);
                _deltaGameObjects = InGameObjectFinder.GetInstance().ComputeNormalizedPathBasedDeltaCounts(priorGameObjectStatus, gameObjectTransforms, out _);
            }
        }

        // Track counts from the last keyframe completion and use that as the 'prior' data
        public static List<bool> Matched(List<KeyFrameCriteria> criteriaList, Dictionary<int, TransformStatus> priorUIStatus, Dictionary<int, TransformStatus> priorGameObjectStatus, Dictionary<int, TransformStatus> uiTransforms, Dictionary<int, TransformStatus> gameObjectTransforms)
        {
            UpdateDeltaCounts(priorUIStatus, priorGameObjectStatus, uiTransforms, gameObjectTransforms);

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
                    var criteriaPathData = criteria.data as PathKeyFrameCriteriaData;
                    var normalizedPath = criteriaPathData.path;
                    var pathHash = normalizedPath.GetHashCode();
                    // see if it is in the UI path
                    if (_deltaUI.TryGetValue(pathHash, out var uiObjectCounts))
                    {
                        // compare counts for match
                        switch (criteriaPathData.countRule)
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
                                if (uiObjectCounts.count >= criteriaPathData.count)
                                {
                                    matched = true;
                                }
                                break;
                            case CountRule.LessThanEqual:
                                if (uiObjectCounts.count <= criteriaPathData.count)
                                {
                                    matched = true;
                                }
                                break;
                        }

                        // then evaluate added / removed data
                        if (matched && uiObjectCounts.addedCount < criteriaPathData.addedCount)
                        {
                            matched = false;
                        }

                        if (matched && uiObjectCounts.removedCount < criteriaPathData.removedCount)
                        {
                            matched = false;
                        }
                    }
                    else if (_deltaGameObjects.TryGetValue(pathHash, out var gameObjectCounts))
                    {
                        // compare counts for match
                        switch (criteriaPathData.countRule)
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
                                if (gameObjectCounts.count >= criteriaPathData.count)
                                {
                                    matched = true;
                                }
                                break;
                            case CountRule.LessThanEqual:
                                if (gameObjectCounts.count <= criteriaPathData.count)
                                {
                                    matched = true;
                                }
                                break;
                        }

                        // then evaluate added / removed data need equal to or more of each to pass
                        if (matched && gameObjectCounts.addedCount < criteriaPathData.addedCount)
                        {
                            matched = false;
                        }

                        if (matched && gameObjectCounts.removedCount < criteriaPathData.removedCount)
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
