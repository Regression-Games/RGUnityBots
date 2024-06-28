using System.Collections.Generic;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments
{
    public static class NormalizedPathCriteriaEvaluator
    {

        private static List<Dictionary<long, PathBasedDeltaCount>> _deltasList = new();

        private static int _lastFrameEvaluated = -1;

        private static void UpdateDeltaCounts(Dictionary<long, ObjectStatus> priorTransformsStatus, Dictionary<long, ObjectStatus> priorEntitiesStatus, Dictionary<long, ObjectStatus> transformsStatus, Dictionary<long, ObjectStatus> entitiesStatus)
        {
            var currentFrameCount = Time.frameCount;
            if (_lastFrameEvaluated != currentFrameCount)
            {
                _lastFrameEvaluated = currentFrameCount;
                //Compute the frame deltas (Use InGameObjectFinder)... but only once per frame
                _deltasList.Clear();
                var objectFinders = Object.FindObjectsByType<ObjectFinder>(FindObjectsSortMode.None);
                foreach (var objectFinder in objectFinders)
                {
                    if (objectFinder is TransformObjectFinder)
                    {
                        _deltasList.Add(objectFinder.ComputeNormalizedPathBasedDeltaCounts(priorTransformsStatus, transformsStatus, out _));
                    }
                    else
                    {
                        // we only support 2 , transforms and entities
                        _deltasList.Add(objectFinder.ComputeNormalizedPathBasedDeltaCounts(priorEntitiesStatus, entitiesStatus, out _));
                    }
                }
            }
        }

        // Track counts from the last keyframe completion and use that as the 'prior' data
        public static List<string> Matched(int segmentNumber, List<KeyFrameCriteria> criteriaList, Dictionary<long, ObjectStatus> priorTransformsStatus, Dictionary<long, ObjectStatus> priorEntitiesStatus, Dictionary<long, ObjectStatus> transformsStatus, Dictionary<long, ObjectStatus> entitiesStatus)
        {
            UpdateDeltaCounts(priorTransformsStatus, priorEntitiesStatus, transformsStatus, entitiesStatus);

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
                    var deltasListCount = _deltasList.Count;
                    PathBasedDeltaCount objectCounts = null;
                    for (var i = 0; objectCounts == null && i < deltasListCount; i++)
                    {
                        var deltas = _deltasList[i];
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
