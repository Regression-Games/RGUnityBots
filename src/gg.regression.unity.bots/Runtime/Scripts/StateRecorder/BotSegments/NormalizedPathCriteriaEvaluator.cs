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
        public static List<string> Matched(List<KeyFrameCriteria> criteriaList, Dictionary<int, TransformStatus> priorUIStatus, Dictionary<int, TransformStatus> priorGameObjectStatus, Dictionary<int, TransformStatus> uiTransforms, Dictionary<int, TransformStatus> gameObjectTransforms)
        {
            UpdateDeltaCounts(priorUIStatus, priorGameObjectStatus, uiTransforms, gameObjectTransforms);

            var resultList = new List<string>();
            foreach (var criteria in criteriaList)
            {
                string matched = null; // null == matched, error message if not matched
                if (criteria.transient && criteria.Replay_TransientMatched)
                {
                    matched = null;
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
                                if (uiObjectCounts.count != 0)
                                {
                                    matched = $"NormalizedPath (UI) - {normalizedPath} - CountRule.Zero - actual: {uiObjectCounts.count}";
                                }
                                break;
                            case CountRule.NonZero:
                                if (uiObjectCounts.count <= 0)
                                {
                                    matched = $"NormalizedPath (UI) - {normalizedPath} - CountRule.NonZero - actual: {uiObjectCounts.count}";
                                }
                                break;
                            case CountRule.GreaterThanEqual:
                                if (uiObjectCounts.count < criteriaPathData.count)
                                {
                                    matched = $"NormalizedPath (UI) - {normalizedPath} - CountRule.GreaterThanEqual - actual: {uiObjectCounts.count} , expected: {criteriaPathData.count}";
                                }
                                break;
                            case CountRule.LessThanEqual:
                                if (uiObjectCounts.count > criteriaPathData.count)
                                {
                                    matched = $"NormalizedPath (UI) - {normalizedPath} - CountRule.LessThanEqual - actual: {uiObjectCounts.count} , expected: {criteriaPathData.count}";
                                }
                                break;
                        }

                        // then evaluate added / removed data
                        if (matched == null && uiObjectCounts.addedCount < criteriaPathData.addedCount)
                        {
                            matched = $"NormalizedPath (UI) - {normalizedPath} - addedCount - actual: {uiObjectCounts.addedCount} , expected: {criteriaPathData.addedCount}";
                        }

                        if (matched == null && uiObjectCounts.removedCount < criteriaPathData.removedCount)
                        {
                            matched = $"NormalizedPath (UI) - {normalizedPath} - removedCount - actual: {uiObjectCounts.removedCount} , expected: {criteriaPathData.removedCount}";
                        }
                    }
                    else if (_deltaGameObjects.TryGetValue(pathHash, out var gameObjectCounts))
                    {
                        // compare counts for match
                        switch (criteriaPathData.countRule)
                        {
                            case CountRule.Zero:
                                if (gameObjectCounts.count != 0)
                                {
                                    matched = $"NormalizedPath (WorldSpace) - {normalizedPath} - CountRule.Zero - actual: {gameObjectCounts.count}";
                                }
                                break;
                            case CountRule.NonZero:
                                if (gameObjectCounts.count <= 0)
                                {
                                    matched = $"NormalizedPath (WorldSpace) - {normalizedPath} - CountRule.NonZero - actual: {gameObjectCounts.count}";
                                }
                                break;
                            case CountRule.GreaterThanEqual:
                                if (gameObjectCounts.count < criteriaPathData.count)
                                {
                                    matched = $"NormalizedPath (WorldSpace) - {normalizedPath} - CountRule.GreaterThanEqual - actual: {gameObjectCounts.count} , expected: {criteriaPathData.count}";
                                }
                                break;
                            case CountRule.LessThanEqual:
                                if (gameObjectCounts.count > criteriaPathData.count)
                                {
                                    matched = $"NormalizedPath (WorldSpace) - {normalizedPath} - CountRule.LessThanEqual - actual: {gameObjectCounts.count} , expected: {criteriaPathData.count}";
                                }
                                break;
                        }

                        // then evaluate added / removed data
                        if (matched == null && gameObjectCounts.addedCount < criteriaPathData.addedCount)
                        {
                            matched = $"NormalizedPath (WorldSpace) - {normalizedPath} - addedCount - actual: {gameObjectCounts.addedCount} , expected: {criteriaPathData.addedCount}";
                        }

                        if (matched == null && gameObjectCounts.removedCount < criteriaPathData.removedCount)
                        {
                            matched = $"NormalizedPath (WorldSpace) - {normalizedPath} - removedCount - actual: {gameObjectCounts.removedCount} , expected: {criteriaPathData.removedCount}";
                        }
                    }
                    else
                    {
                        // else - criteria not met - false
                        matched = $"NormalizedPath (WorldSpace) - {normalizedPath} - missing object";
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
