using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.BotCriteria;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments
{
    public sealed class KeyFrameEvaluator
    {
        public static readonly KeyFrameEvaluator Evaluator = new ();

        private static Dictionary<long, ObjectStatus> _priorKeyFrameTransformStatus = new ();
        private static Dictionary<long, ObjectStatus> _priorKeyFrameEntityStatus = new ();

        public void Reset()
        {
            _unmatchedCriteria.Clear();
            _newUnmatchedCriteria.Clear();
            _priorKeyFrameTransformStatus.Clear();
            _priorKeyFrameEntityStatus.Clear();
            CVTextCriteriaEvaluator.Reset();
            CVImageCriteriaEvaluator.Reset();
        }

        public string GetUnmatchedCriteria()
        {
            if (_unmatchedCriteria.Count == 0)
            {
                return null;
            }

            var sb = new StringBuilder(1000);
            var unmatchedCriteriaCount = _unmatchedCriteria.Count;
            for (var i = 0; i < unmatchedCriteriaCount; i++)
            {
                sb.Append(_unmatchedCriteria[i]);
                if (i + 1 < unmatchedCriteriaCount)
                {
                    sb.Append("\r\n");
                }
            }

            return sb.ToString();
        }

        private List<string> _unmatchedCriteria = new(1000);
        private List<string> _newUnmatchedCriteria = new(1000);

        /**
         * <summary>Called to tell this to persist the current frame as the last successful key frame.  This is NOT done automatically in matched because we process multiple transient frames in a single pass and you don't want to update to a newer state yet when a later transient frame passes before the earlier one.</summary>
         */
        public void PersistPriorFrameStatus()
        {
            var objectFinders = Object.FindObjectsByType<ObjectFinder>(FindObjectsSortMode.None);
            var hadEntities = false;
            foreach (var objectFinder in objectFinders)
            {
                if (objectFinder is TransformObjectFinder)
                {
                    // copy the dictionary
                    _priorKeyFrameTransformStatus = objectFinder.GetObjectStatusForCurrentFrame().Item2.ToDictionary((pair => pair.Key), (pair => pair.Value));
                }
                else
                {
                    // copy the dictionary
                    hadEntities = true;
                    _priorKeyFrameEntityStatus = objectFinder.GetObjectStatusForCurrentFrame().Item2.ToDictionary((pair => pair.Key), (pair => pair.Value));
                }
            }

            if (!hadEntities)
            {
                _priorKeyFrameEntityStatus = new();
            }

        }

        /**
         * <summary>Publicly callable.. caches the statuses of the last passed key frame for computing delta counts from</summary>
         */
        public bool Matched(bool firstSegment, int segmentNumber, bool botActionCompleted, List<KeyFrameCriteria> criteriaList)
        {
            _newUnmatchedCriteria.Clear();
            bool matched = MatchedHelper(firstSegment, segmentNumber, botActionCompleted, BooleanCriteria.And, criteriaList);
            if (matched)
            {
                CVTextCriteriaEvaluator.Cleanup(segmentNumber);
                _unmatchedCriteria.Clear();
                _newUnmatchedCriteria.Clear();
            }
            else
            {
                (_unmatchedCriteria, _newUnmatchedCriteria) = (_newUnmatchedCriteria, _unmatchedCriteria);
                _newUnmatchedCriteria.Clear();
            }
            return matched;
        }

        private static readonly List<Dictionary<long, PathBasedDeltaCount>> DeltaCounts = new ();
        private static int _lastFrameEvaluated = -1;


        /**
         * <summary>Only to be called internally by KeyFrameEvaluator. firstSegment represents if this is the first segment in the current pass's list of segments to evaluate</summary>
         */
        internal bool MatchedHelper(bool firstSegment, int segmentNumber, bool botActionCompleted, BooleanCriteria andOr, List<KeyFrameCriteria> criteriaList)
        {
            var objectFinders = Object.FindObjectsByType<ObjectFinder>(FindObjectsSortMode.None);
            var currentFrameCount = Time.frameCount;
            if (_lastFrameEvaluated != currentFrameCount)
            {
                _lastFrameEvaluated = currentFrameCount;
                DeltaCounts.Clear();
                foreach (var objectFinder in objectFinders)
                {
                    if (objectFinder is TransformObjectFinder)
                    {
                        var transformsStatus = objectFinder.GetObjectStatusForCurrentFrame().Item2;
                        var hashes = objectFinder.ComputeNormalizedPathBasedDeltaCounts(_priorKeyFrameTransformStatus, transformsStatus, out _);
                        DeltaCounts.Add(hashes);
                    }
                    else
                    {
                        var entitiesStatus = objectFinder.GetObjectStatusForCurrentFrame().Item2;
                        var hashes = objectFinder.ComputeNormalizedPathBasedDeltaCounts(_priorKeyFrameEntityStatus, entitiesStatus, out _);
                        DeltaCounts.Add(hashes);
                    }
                }
            }

            var partialNormalizedPathsToMatch = new List<KeyFrameCriteria>();
            var normalizedPathsToMatch = new List<KeyFrameCriteria>();
            var orsToMatch = new List<KeyFrameCriteria>();
            var andsToMatch = new List<KeyFrameCriteria>();
            var cvTextsToMatch = new List<KeyFrameCriteria>();
            var cvImagesToMatch = new List<KeyFrameCriteria>();
            var cvObjectsToMatch = new List<KeyFrameCriteria>();

            var length = criteriaList.Count;
            for (var i = 0; i < length; i++)
            {
                var entry = criteriaList[i];
                if (entry.transient && entry.Replay_TransientMatched)
                {
                    if (andOr == BooleanCriteria.Or)
                    {
                        return true;
                    }
                    continue;
                }

                switch (entry.type)
                {
                    case KeyFrameCriteriaType.And:
                        andsToMatch.Add(entry);
                        break;
                    case KeyFrameCriteriaType.Or:
                        orsToMatch.Add(entry);
                        break;
                    case KeyFrameCriteriaType.NormalizedPath:
                        normalizedPathsToMatch.Add(entry);
                        break;
                    case KeyFrameCriteriaType.PartialNormalizedPath:
                        partialNormalizedPathsToMatch.Add(entry);
                        break;
                    case KeyFrameCriteriaType.UIPixelHash:
                        // only check the pixel hash change on the first segment being evaluated so we don't pre-emptively pass on future segments that should return false until they are first in the list
                        if (firstSegment && (entry.Replay_TransientMatched || GameFacePixelHashObserver.GetInstance().HasPixelHashChanged()))
                        {
                            entry.Replay_TransientMatched = true;
                        }
                        else
                        {
                            _newUnmatchedCriteria.Add("UIPixelHash has not changed");
                            return false;
                        }
                        break;
                    case KeyFrameCriteriaType.CVText:
                        cvTextsToMatch.Add(entry);
                        break;
                     case KeyFrameCriteriaType.CVImage:
                        cvImagesToMatch.Add(entry);
                        break;
                    case KeyFrameCriteriaType.CVObjectDetection:
                        cvObjectsToMatch.Add(entry);
                        break;
                    case KeyFrameCriteriaType.ActionComplete:
                        if (!botActionCompleted)
                        {
                            _newUnmatchedCriteria.Add("Waiting for action to complete...");
                            return false;
                        }
                        break;
                }
            }

            // process each list.. start with the ones for this tier
            // start cv stuff first.. it runs async anyway, but the sooner we can start it the better
            if (cvObjectsToMatch.Count > 0)
            {
                var cvObjectDetectionResults = CVObjectDetectionEvaluator.Matched(segmentNumber, cvObjectsToMatch);
                if (cvObjectDetectionResults == null)
                {
                    // cvText results will be null until we get the async response back
                    if (andOr == BooleanCriteria.And)
                    {
                        _newUnmatchedCriteria.Add("Waiting for CV Object Detection evaluation results ...");
                        return false;
                    }
                }
                else
                {
                    var cvObjectDetectionResultsCount = cvObjectDetectionResults.Count;
                    for (var i = 0; i < cvObjectDetectionResultsCount; i++)
                    {
                        var resultEntry = cvObjectDetectionResults[i];
                        if (resultEntry == null)
                        {
                            if (andOr == BooleanCriteria.Or)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            if (andOr == BooleanCriteria.And)
                            {
                                _newUnmatchedCriteria.Add(resultEntry);
                                return false;
                            }
                        }
                    }
                }
            }

            if (cvTextsToMatch.Count > 0)
            {
                var cvTextResults = CVTextCriteriaEvaluator.Matched(segmentNumber, cvTextsToMatch);
                if (cvTextResults == null)
                {
                    // cvText results will be null until we get the async response back
                    if (andOr == BooleanCriteria.And)
                    {
                        _newUnmatchedCriteria.Add("Waiting for CVText evaluation results ...");
                        return false;
                    }
                }
                else
                {
                    var cvTextResultsCount = cvTextResults.Count;
                    for (var i = 0; i < cvTextResultsCount; i++)
                    {
                        var resultEntry = cvTextResults[i];
                        if (resultEntry == null)
                        {
                            if (andOr == BooleanCriteria.Or)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            if (andOr == BooleanCriteria.And)
                            {
                                _newUnmatchedCriteria.Add(resultEntry);
                                return false;
                            }
                        }
                    }
                }
            }

            if (cvImagesToMatch.Count > 0)
            {
                var cvImageResults = CVImageCriteriaEvaluator.Matched(segmentNumber, cvImagesToMatch);
                if (cvImageResults == null)
                {
                    // cvText results will be null until we get the async response back
                    if (andOr == BooleanCriteria.And)
                    {
                        _newUnmatchedCriteria.Add("Waiting for CVImage evaluation results ...");
                        return false;
                    }
                }
                else
                {
                    var cvImageResultsCount = cvImageResults.Count;
                    for (var i = 0; i < cvImageResultsCount; i++)
                    {
                        var resultEntry = cvImageResults[i];
                        if (resultEntry == null)
                        {
                            if (andOr == BooleanCriteria.Or)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            if (andOr == BooleanCriteria.And)
                            {
                                _newUnmatchedCriteria.Add(resultEntry);
                                return false;
                            }
                        }
                    }
                }
            }

            // we do this before the partial as this one is far more efficient computationally..
            if (normalizedPathsToMatch.Count > 0)
            {
                var pathResults = NormalizedPathCriteriaEvaluator.Matched(segmentNumber, normalizedPathsToMatch, DeltaCounts);
                var pathResultsCount = pathResults.Count;
                for (var j = 0; j < pathResultsCount; j++)
                {
                    var pathEntry = pathResults[j];
                    if (pathEntry == null)
                    {
                        if (andOr == BooleanCriteria.Or)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (andOr == BooleanCriteria.And)
                        {
                            _newUnmatchedCriteria.Add(pathEntry);
                            return false;
                        }
                    }
                }
            }

            // process each list.. start with the ones for this tier
            if (partialNormalizedPathsToMatch.Count > 0)
            {
                var pathResults = PartialNormalizedPathCriteriaEvaluator.Matched(segmentNumber, partialNormalizedPathsToMatch, DeltaCounts);
                var pathResultsCount = pathResults.Count;
                for (var j = 0; j < pathResultsCount; j++)
                {
                    var pathEntry = pathResults[j];
                    if (pathEntry == null)
                    {
                        if (andOr == BooleanCriteria.Or)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (andOr == BooleanCriteria.And)
                        {
                            _newUnmatchedCriteria.Add(pathEntry);
                            return false;
                        }
                    }
                }
            }

            var orCount = orsToMatch.Count;
            if (orCount > 0)
            {
                for (var j = 0; j < orCount; j++)
                {
                    var orEntry = orsToMatch[j];
                    var m = OrKeyFrameCriteriaEvaluator.Matched(firstSegment, segmentNumber, botActionCompleted, orEntry);
                    if (m)
                    {
                        if (andOr == BooleanCriteria.Or)
                        {
                            return true;
                        }

                        orEntry.Replay_TransientMatched = true;
                    }
                    else
                    {
                        if (andOr == BooleanCriteria.And)
                        {
                            return false;
                        }
                    }
                }
            }

            var andCount = andsToMatch.Count;
            if (andCount > 0)
            {
                for (var j = 0; j < andCount; j++)
                {
                    var andEntry = andsToMatch[j];
                    var m = AndKeyFrameCriteriaEvaluator.Matched(firstSegment, segmentNumber, botActionCompleted, andEntry);
                    if (m)
                    {
                        if (andOr == BooleanCriteria.Or)
                        {
                            return true;
                        }

                        andEntry.Replay_TransientMatched = true;
                    }
                    else
                    {
                        if (andOr == BooleanCriteria.And)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;

        }
    }
}
