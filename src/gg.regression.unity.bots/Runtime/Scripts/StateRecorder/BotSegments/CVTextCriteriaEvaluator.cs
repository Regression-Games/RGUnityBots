using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.CVService;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace RegressionGames.StateRecorder.BotSegments
{


    /**
     * <summary>Evaluates CV Text criteria using CVServiceManager to send/receive HTTP requests to a python server for doing the actual CV evaluations.
     * Python 'detects' text in a provided image and then this class evaluates those results against the specified bot segment criteria.</summary>
     */
    public static class CVTextCriteriaEvaluator
    {
        // This class uses explicit locking for thread safety as we have multiple different threads affecting the state of the tracking dictionaries, as well as async web responses
        // We manage locks by locking just on the _requestTracker for access to both dictionaries

        // if an entry is missing, no request for that segment in progress
        private static readonly Dictionary<int, Action> _requestTracker = new();

        // if an entry is NULL, request is in progress for that segment
        // if an entry has a value, then it is completed for that segment.. it should be cleared out on the next matched call if the result didn't match so it can run again
        private static readonly Dictionary<int, List<CVTextResult>> _resultTracker = new();

        private static readonly Dictionary<int, List<string>> _priorResultsTracker = new();

        public static void Reset()
        {
            lock (_requestTracker)
            {
                foreach (var keyValuePair in _requestTracker.Where((pair => pair.Value != null)))
                {
                    RGDebug.LogDebug($"CVTextCriteriaEvaluator - Reset - botSegment: {keyValuePair.Key} - abortingWebRequest");
                    keyValuePair.Value.Invoke();
                }

                _requestTracker.Clear();
                _resultTracker.Clear();
                _priorResultsTracker.Clear();
            }
        }


        // cleanup async and results tracking for that segment
        public static void Cleanup(int segmentNumber)
        {
            RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Cleanup - botSegment: {segmentNumber} - BEGIN");
            lock (_requestTracker)
            {
                RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Cleanup - botSegment: {segmentNumber} - insideLock");
                if (_requestTracker.Remove(segmentNumber, out var request))
                {
                    try
                    {
                        RGDebug.LogDebug($"CVTextCriteriaEvaluator - Cleanup - botSegment: {segmentNumber} - abortingWebRequest");
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
                _priorResultsTracker.Remove(segmentNumber, out _);
                RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Cleanup - botSegment: {segmentNumber} - END");
            }
        }

        // Returns a list of non-matched entries
        public static List<string> Matched(int segmentNumber, List<KeyFrameCriteria> criteriaList)
        {
            RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - BEGIN");
            var resultList = new List<string>();

            List<CVTextResult> cvTextResults = null;
            List<string> priorResults = null;
            lock (_requestTracker)
            {
                _resultTracker.TryGetValue(segmentNumber, out cvTextResults);
                _priorResultsTracker.TryGetValue(segmentNumber, out priorResults);
                var resultsString = cvTextResults == null ? "null":$"\n[{string.Join(",\n", cvTextResults)}]";
                if (cvTextResults != null)
                {
                    RGDebug.LogDebug($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - cvTextResults: {resultsString}");
                }
            }

            if (priorResults == null && cvTextResults != null)
            {
                // Evaluate the results if we have some

                // Detailed evaluation of the results vs our criteria, this is currently n^3 (loop in a loop in a loop), but needs to be due to allowing partial word matching
                // we do our best to make this faster by removing from the lists as we match to reduce future iterations
                for (var i = criteriaList.Count - 1; i >= 0; i--)
                {
                    var criteria = criteriaList[i];
                    if (!criteria.transient || !criteria.Replay_TransientMatched)
                    {
                        var criteriaData = criteria.data as CVTextKeyFrameCriteriaData;

                        // setup a tracker of each text part and whether it is found.. remove from this as we match to improve performance and as a way of marking what was found
                        // We treat a criteria multi word phrase as separate words that must ALL exist within the defined rect
                        // If no rect is defined, all the words must simply exist on the screen, but their relative positions are then not considered
                        var textParts = criteriaData.text.Trim().Split(' ').Select(a => a.Trim()).Where(a => a.Length > 0).ToList();

                        var cvTextResultsCount = cvTextResults.Count;
                        for (var k = 0; k < cvTextResultsCount && textParts.Count > 0; k++)
                        {
                            var cvTextResult = cvTextResults[k];
                            var cvText = cvTextResult.text.Trim();

                            if (criteriaData.textCaseRule == TextCaseRule.Ignore)
                            {
                                cvText = cvText.ToLower();
                            }

                            for (var j = textParts.Count - 1; j >= 0; j--)
                            {
                                var matched = false;
                                var textToMatch = textParts[j];

                                if (criteriaData.textCaseRule == TextCaseRule.Ignore)
                                {
                                    textToMatch = textToMatch.ToLower();
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
                                    if (criteriaData.withinRect == null)
                                    {
                                        textParts.RemoveAt(j); // remove the one we matched so we don't have to check it again
                                        break;
                                    }
                                    else
                                    {
                                        // ensure result rect is inside
                                        var withinRect = criteriaData.withinRect;

                                        var relativeScaling = new Vector2(withinRect.screenSize.x / (float)cvTextResult.resolution.x, withinRect.screenSize.y / (float)cvTextResult.resolution.y);

                                        // check the bottom left and top right to see if it intersects our rect
                                        var bottomLeft = new Vector2Int(Mathf.CeilToInt(cvTextResult.rect.x * relativeScaling.x), Mathf.CeilToInt(cvTextResult.rect.y * relativeScaling.y));
                                        var topRight = new Vector2Int(bottomLeft.x + Mathf.FloorToInt(cvTextResult.rect.width * relativeScaling.x), bottomLeft.y + Mathf.FloorToInt(cvTextResult.rect.height * relativeScaling.y));

                                        // we currently test overlap, should we test fully inside instead ??
                                        if (withinRect.rect.Contains(bottomLeft) || withinRect.rect.Contains(topRight))
                                        {
                                            textParts.RemoveAt(j); // remove the one we matched so we don't have to check it again
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (textParts.Count != 0)
                        {
                            resultList.Add($"Missing CVText - text: {criteriaData.text.Trim()}, caseRule: {criteriaData.textCaseRule}, matchRule: {criteriaData.textMatchingRule}, missingWords: [{string.Join(',',textParts)}]");
                        }
                        else
                        {
                            lock (_requestTracker)
                            {
                                _priorResultsTracker[segmentNumber] = new();
                                criteria.Replay_TransientMatched = true;
                            }
                        }
                    }
                }

                // clear out the result as we've evaluated it as failed and need to get a new result
                if (resultList.Count > 0)
                {
                    lock (_requestTracker)
                    {
                        _priorResultsTracker[segmentNumber] = resultList;
                    }
                }

                // clear out the result as we've evaluated it as failed and need to get a new result
                if (resultList.Count > 0)
                {
                    lock (_requestTracker)
                    {
                        _resultTracker.Remove(segmentNumber, out _);
                    }
                }
            }
            else
            {
                resultList.Add("Awaiting CVText evaluation result from server...");
            }

            RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - resultList: {resultList.Count} - END");

            RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - resultList: {resultList.Count} - END");

            if (priorResults is not { Count: 0 } || cvTextResults == null)
            {
                // Handle possibly starting the web request for evaluation
                var requestInProgress = _requestTracker.TryGetValue(segmentNumber, out _);
                if (!requestInProgress)
                {
                    // double checked locking paradigm to avoid race conditions from multiple threads while still optimizing for the repeated call path not having to lock
                    lock (_requestTracker)
                    {
                        requestInProgress = _requestTracker.TryGetValue(segmentNumber, out _);
                        if (!requestInProgress)
                        {
                            var imageData = ScreenshotCapture.GetCurrentScreenshot(segmentNumber, out var width, out var height);
                            if (imageData != null)
                            {
                                // mark a request in progress inside the lock to avoid race conditions.. must be done before starting async process
                                // mark that we are in progress by putting entries in the dictionary of null until we replace with the real data
                                // thus contains key returns true
                                _requestTracker[segmentNumber] = null;
                                _resultTracker[segmentNumber] = null;

                                // do NOT await this, let it run async
                                _ = CVServiceManager.GetInstance().PostCriteriaTextDiscover(
                                    new CVTextCriteriaRequest()
                                    {
                                        screenshot = new CVImageBinaryData()
                                        {
                                            width = width,
                                            height = height,
                                            data = imageData
                                        }
                                    },
                                    abortRegistrationHook: action =>
                                    {
                                        RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - abortHook registration callback");
                                        lock (_requestTracker)
                                        {
                                            RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - abortHook registration callback - insideLock");
                                            // make sure we haven't already cleaned this up
                                            if (_requestTracker.ContainsKey(segmentNumber))
                                            {
                                                _requestTracker[segmentNumber] = action;
                                            }
                                        }
                                    },
                                    onSuccess: list =>
                                    {
                                        RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - onSuccess callback");
                                        lock (_requestTracker)
                                        {
                                            RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - onSuccess callback - insideLock");
                                            // make sure we haven't already cleaned this up
                                            if (_resultTracker.ContainsKey(segmentNumber))
                                            {
                                                RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - onSuccess callback - storingResult");
                                                // store the result
                                                _resultTracker[segmentNumber] = list;
                                                // cleanup the request tracker
                                                _requestTracker.Remove(segmentNumber);
                                                _priorResultsTracker.Remove(segmentNumber);
                                            }
                                        }
                                    },
                                    onFailure: () =>
                                    {
                                        RGDebug.LogWarning($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - onFailure callback - failure invoking CVService text criteria evaluation");
                                        lock (_requestTracker)
                                        {
                                            RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - onFailure callback - insideLock");
                                            // make sure we haven't already cleaned this up
                                            if (_resultTracker.ContainsKey(segmentNumber))
                                            {
                                                RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - onFailure callback - storingResult");
                                                // store the result as empty so we know we finished, but won't pass
                                                _resultTracker[segmentNumber] = new();
                                                // cleanup the request tracker
                                                _requestTracker.Remove(segmentNumber);
                                                _priorResultsTracker.Remove(segmentNumber);
                                            }
                                        }
                                    });
                                RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - Request - SENT");
                            }
                            else
                            {
                                resultList.Add("Awaiting screenshot data ...");
                            }
                        }
                    }
                }

                if (requestInProgress)
                {
                    if (priorResults == null )
                    {
                        resultList.Add("Awaiting CV text evaluation result ...");
                    }
                    else
                    {
                        // show the prior failures
                        resultList.AddRange(priorResults);
                    }
                }
            }

            RGDebug.LogVerbose($"CVTextCriteriaEvaluator - Matched - botSegment: {segmentNumber} - resultList: {resultList.Count} - END");

            return resultList;
        }
    }
}
