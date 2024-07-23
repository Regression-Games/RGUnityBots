using System;
using System.Collections.Generic;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments
{
    // This class uses explicit locking for thread safety as we have multiple different threads affecting the state of the tracking dictionaries, as well as async web responses
    public static class CVTextCriteriaEvaluator
    {

        // We manage locks by locking just on the _requestTracker for access to both dictionaries

        // if an entry is missing, no request for that segment in progress
        private static readonly Dictionary<int, Action> _requestTracker;

        // if an entry is NULL, request is in progress for that segment
        // if an entry has a value, then it is completed for that segment.. it should be cleared out on the next matched call if the result didn't match so it can run again
        private static readonly Dictionary<int, List<CVTextResult>> _resultTracker;


        // cleanup async and results tracking for that segment
        public static void Cleanup(int segmentNumber)
        {
            RGDebug.LogInfo($"CVTextCriteriaEvaluator - Cleanup - botSegment: {segmentNumber} - BEGIN");
            lock (_requestTracker)
            {
                RGDebug.LogInfo($"CVTextCriteriaEvaluator - Cleanup - botSegment: {segmentNumber} - insideLock");
                if (_requestTracker.Remove(segmentNumber, out var request))
                {
                    try
                    {
                        RGDebug.LogInfo($"CVTextCriteriaEvaluator - Cleanup - botSegment: {segmentNumber} - abortingWebRequest");
                        //try to abort the request
                        request.Invoke();
                    }
                    catch (Exception)
                    {
                        // DO NOTHING .. we tried.. we really did
                    }
                }

                // remove the tracked result
                _resultTracker.Remove(segmentNumber, out _);
                RGDebug.LogInfo($"CVTextCriteriaEvaluator - Cleanup - botSegment: {segmentNumber} - END");
            }
        }

        // Track counts from the last keyframe completion and use that as the 'prior' data
        // Returns a list of non-matched entries
        public static List<string> Matched(int segmentNumber, List<KeyFrameCriteria> criteriaList, List<Dictionary<long, PathBasedDeltaCount>> deltaCounts, CVImageRequestData screenshotData)
        {
            RGDebug.LogInfo($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - BEGIN");
            var resultList = new List<string>();
            var shouldStartRequest = false;
            var requestInProgress = _requestTracker.TryGetValue(segmentNumber, out _);

            if (!requestInProgress)
            {
                // double checked locking paradigm to avoid race conditions from multiple threads while still optimizing for the repeated call path not having to lock
                lock (_requestTracker)
                {
                    requestInProgress = _requestTracker.TryGetValue(segmentNumber, out _);
                    if (!requestInProgress)
                    {
                        // mark a request in progress inside the lock to avoid race conditions
                        // mark that we are in progress by putting entries in the dictionary
                        _requestTracker[segmentNumber] = null;
                        _resultTracker[segmentNumber] = null;
                        shouldStartRequest = true;
                    }
                }
            }

            if (shouldStartRequest)
            {
                // do NOT await this, let it run async
                _ = CVServiceManager.GetInstance().PostCriteriaTextDiscover(
                    new CVTextCriteriaRequest()
                    {
                        screenshot = screenshotData
                    },
                    abortHook: action =>
                    {
                        RGDebug.LogInfo($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - abortHook callback");
                        lock (_requestTracker)
                        {
                            RGDebug.LogInfo($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - abortHook callback - insideLock");
                            // make sure we haven't already cleaned this up
                            if (_requestTracker.ContainsKey(segmentNumber))
                            {
                                _requestTracker[segmentNumber] = action;
                            }
                        }
                    },
                    onSuccess: list =>
                    {
                        RGDebug.LogInfo($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - onSuccess callback");
                        lock (_requestTracker)
                        {
                            RGDebug.LogInfo($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - onSuccess callback - insideLock");
                            // make sure we haven't already cleaned this up
                            if (_resultTracker.ContainsKey(segmentNumber))
                            {
                                RGDebug.LogInfo($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - onSuccess callback - storingResult");
                                // store the result
                                _resultTracker[segmentNumber] = list;
                                // cleanup the request tracker
                                _requestTracker.Remove(segmentNumber);
                            }
                        }
                    },
                    onFailure: () =>
                    {
                        RGDebug.LogWarning("CVTextCriteriaEvaluator - failure invoking CVService text criteria evaluation");
                    });
                RGDebug.LogInfo($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - SENT");
            }

            var hasResults = false;
            List<CVTextResult> cvTextResults = null;
            lock (_requestTracker)
            {
                hasResults = _resultTracker.TryGetValue(segmentNumber, out cvTextResults);
                var resultsString = cvTextResults == null ? "null":$"\n[{string.Join(",\n", cvTextResults)}]";
                RGDebug.LogInfo($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - hasResults: {(hasResults?"true":"false")} - cvTextResults: {resultsString}");
            }

            if (hasResults)
            {
                // Detailed evaluation of the results vs our criteria, this is currently n^2 (loop in a loop), but somewhat needs to be due to allowing partial word matching
                var criteriaListCount = criteriaList.Count;
                for (var i = 0; i < criteriaListCount; i++)
                {
                    var criteria = criteriaList[i];
                    if (!criteria.transient || !criteria.Replay_TransientMatched)
                    {
                        var criteriaData = criteria.data as CVTextKeyFrameCriteriaData;
                        var textToMatch = criteriaData.text.Trim();
                        bool matched = false;
                        bool? rectMatched = null;
                        foreach (var cvTextResult in cvTextResults)
                        {
                            matched = false;
                            rectMatched = null;

                            var cvText = cvTextResult.text.Trim();

                            if (criteriaData.textCaseRule == TextCaseRule.Ignore)
                            {
                                textToMatch = textToMatch.ToLower();
                                cvText = cvText.ToLower();
                            }

                            switch (criteriaData.textMatchingRule)
                            {
                                case TextMatchingRule.Matches:
                                    if (textToMatch.Equals(cvText))
                                    {
                                        matched = true;
                                    }
                                    break;
                                case TextMatchingRule.Contains:
                                    if (cvText.Contains(textToMatch))
                                    {
                                        matched = true;
                                    }
                                    break;
                            }

                            if (matched)
                            {
                                if (!criteriaData.withinRect.HasValue)
                                {
                                    // all good
                                    break;
                                }
                                else
                                {
                                    // ensure result rect is inside
                                    var withinRect = criteriaData.withinRect.Value;

                                    var relativeScaling = new Vector2(criteriaData.resolution.x / (float)cvTextResult.resolution.x, criteriaData.resolution.y / (float)cvTextResult.resolution.y);

                                    // check the bottom left and top right to see if it intersects our rect
                                    var bottomLeft = new Vector2Int(Mathf.CeilToInt(cvTextResult.rect.x * relativeScaling.x), Mathf.CeilToInt(cvTextResult.rect.y * relativeScaling.y));
                                    var topRight = new Vector2Int(bottomLeft.x + Mathf.FloorToInt(cvTextResult.rect.width * relativeScaling.x), bottomLeft.y + Mathf.FloorToInt(cvTextResult.rect.height * relativeScaling.y));

                                    if (!( withinRect.Contains(bottomLeft) || withinRect.Contains(topRight)))
                                    {
                                        rectMatched = false;
                                    }
                                    else
                                    {
                                        rectMatched = true;
                                        break;
                                    }
                                }
                            }

                        }

                        if (!matched || rectMatched is false)
                        {
                            resultList.Add($"Missing CVText - text: {textToMatch}, caseRule: {criteriaData.textCaseRule}, matchRule: {criteriaData.textMatchingRule}, rectMatched: {(!rectMatched.HasValue?"null":"false")}");
                        }
                        else
                        {
                            criteria.Replay_TransientMatched = true;
                        }
                    }

                }
            }

            RGDebug.LogInfo($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - resultList: {resultList.Count} - END");
            return resultList;
        }
    }
}
