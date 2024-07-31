using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.CVSerice;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments
{

    /**
     * <summary>Evaluates CV Text criteria using CVServiceManager to send/receive HTTP requests to a python server for doing the actual CV evaluations.
     * Python 'detects' text in a provided image and then this class evaluates those results against the specified bot segment criteria.</summary>
     */
    public static class CVImageCriteriaEvaluator
    {
        // This class uses explicit locking for thread safety as we have multiple different threads affecting the state of the tracking dictionaries, as well as async web responses
        // We manage locks by locking just on the _requestTracker for access to both dictionaries

        // if an entry is missing, no request for that segment in progress
        private static readonly Dictionary<int, ConcurrentDictionary<int, Action>> _requestTracker = new();

        // if an entry is NULL, request is in progress for that segment
        // if an entry has a value, then it is completed for that segment.. it should be cleared out on the next matched call if the result didn't match so it can run again
        private static readonly Dictionary<int, ConcurrentDictionary<int, List<CVImageResult>>> _resultTracker = new();

        private static readonly Dictionary<int, List<string>> _priorResultsTracker = new();

        public static void Reset()
        {
            lock (_requestTracker)
            {
                foreach (var keyValuePair in _requestTracker.Where((pair => pair.Value != null)))
                {
                    RGDebug.LogDebug($"CVImageCriteriaEvaluator - Reset - botSegment: {keyValuePair.Key} - abortingWebRequest");
                    var pair = keyValuePair.Value;
                    foreach (var pairValue in pair.Values)
                    {
                        pairValue.Invoke();
                    }
                }

                _requestTracker.Clear();
                _resultTracker.Clear();
                _priorResultsTracker.Clear();
            }
        }


        // cleanup async and results tracking for that segment
        public static void Cleanup(int segmentNumber)
        {
            RGDebug.LogVerbose($"CVImageCriteriaEvaluator - Cleanup - botSegment: {segmentNumber} - BEGIN");
            lock (_requestTracker)
            {
                RGDebug.LogVerbose($"CVImageCriteriaEvaluator - Cleanup - botSegment: {segmentNumber} - insideLock");
                if (_requestTracker.Remove(segmentNumber, out var requests))
                {
                    try
                    {
                        RGDebug.LogDebug($"CVImageCriteriaEvaluator - Cleanup - botSegment: {segmentNumber} - abortingWebRequests");
                        //try to abort the request
                        foreach (var action in requests.Values)
                        {
                            action.Invoke();
                        }
                    }
                    catch (Exception)
                    {
                        // DO NOTHING .. we tried.. we really did
                    }
                }

                // remove the tracked result
                _resultTracker.Remove(segmentNumber, out _);
                _priorResultsTracker.Remove(segmentNumber, out _);
                RGDebug.LogVerbose($"CVImageCriteriaEvaluator - Cleanup - botSegment: {segmentNumber} - END");
            }
        }

        // Returns a list of non-matched entries
        public static List<string> Matched(int segmentNumber, List<KeyFrameCriteria> criteriaList)
        {
            RGDebug.LogVerbose($"CVImageCriteriaEvaluator - Matched - botSegment: {segmentNumber} - BEGIN");
            var resultList = new List<string>();

            ConcurrentDictionary<int,List<CVImageResult>> cvImageResults = null;
            List<string> priorResults = null;
            lock (_requestTracker)
            {
                _resultTracker.TryGetValue(segmentNumber, out cvImageResults);
                _priorResultsTracker.TryGetValue(segmentNumber, out priorResults);
                var resultsString = cvImageResults == null ? "null":$"\n[{string.Join(",\n", cvImageResults.Select(pair => $"{pair.Key}:[{(pair.Value==null?"null":string.Join(",\n", pair.Value))}]"))}]";
                if (cvImageResults != null)
                {
                    RGDebug.LogDebug($"CVImageCriteriaEvaluator - Matched - botSegment: {segmentNumber} - cvImageResults: {resultsString}");
                }
            }

            var criteriaListCount = criteriaList.Count;

            if (priorResults == null && cvImageResults != null && cvImageResults.Count == criteriaListCount && cvImageResults.All(cv => cv.Value != null))
            {
                // Evaluate the results if we have them all for each criteria
                for (var i = criteriaListCount - 1; i >= 0; i--)
                {
                    var criteria = criteriaList[i];
                    var found = false;
                    if (!criteria.transient || !criteria.Replay_TransientMatched)
                    {
                        var criteriaData = criteria.data as CVImageKeyFrameCriteriaData;

                        if (cvImageResults.TryGetValue(i, out var cvImageResultList) && cvImageResultList is { Count: > 0 })
                        {
                            var withinRect = criteriaData.withinRect;
                            // we had the result for this criteria
                            if (withinRect.HasValue)
                            {
                                foreach (var cvImageResult in cvImageResultList)
                                {
                                    // ensure result rect is inside
                                    var relativeScaling = new Vector2(criteriaData.resolution.x / (float)cvImageResult.resolution.x, criteriaData.resolution.y / (float)cvImageResult.resolution.y);

                                    // check the bottom left and top right to see if it intersects our rect
                                    var bottomLeft = new Vector2Int(Mathf.CeilToInt(cvImageResult.rect.x * relativeScaling.x), Mathf.CeilToInt(cvImageResult.rect.y * relativeScaling.y));
                                    var topRight = new Vector2Int(bottomLeft.x + Mathf.FloorToInt(cvImageResult.rect.width * relativeScaling.x), bottomLeft.y + Mathf.FloorToInt(cvImageResult.rect.height * relativeScaling.y));

                                    // we currently test overlap, should we test fully inside instead ??
                                    if (withinRect.Value.Contains(bottomLeft) || withinRect.Value.Contains(topRight))
                                    {
                                        found = true;
                                        break; // we found one, we can stop
                                    }
                                }

                                if (!found)
                                {
                                    resultList.Add($"CV Image result for criteria at index: {i} was not found withinRect: {RectIntJsonConverter.ToJsonString(withinRect.Value)}");
                                }
                            }
                            else
                            {
                                found = true;
                            }
                        }
                        else
                        {
                            resultList.Add($"Missing CV Image result for criteria at index: {i}");
                        }
                    }

                    if (found)
                    {
                        criteria.Replay_TransientMatched = true;
                    }
                }

                lock (_requestTracker)
                {
                    if (resultList.Count == 0)
                    {
                        _priorResultsTracker[segmentNumber] = new();
                    }

                    else
                    {
                        _priorResultsTracker[segmentNumber] = resultList;
                    }
                }

            }

            if (priorResults is not { Count: 0 } || cvImageResults == null)
            {
                // Handle possibly starting the web request for evaluation
                var requestsInProgress = _requestTracker.TryGetValue(segmentNumber, out _);
                if (!requestsInProgress)
                {
                    // double checked locking paradigm to avoid race conditions from multiple threads while still optimizing for the repeated call path not having to lock
                    lock (_requestTracker)
                    {
                        requestsInProgress = _requestTracker.TryGetValue(segmentNumber, out _);
                        if (!requestsInProgress)
                        {
                            var imageData = ScreenshotCapture.GetCurrentScreenshot(segmentNumber, out var width, out var height);
                            if (imageData != null)
                            {
                                // mark a request in progress inside the lock to avoid race conditions.. must be done before starting async process
                                // mark that we are in progress by putting entries in the dictionary of null until we replace with the real data
                                // thus contains key returns true
                                _requestTracker[segmentNumber] = new();
                                _resultTracker[segmentNumber] = new();

                                for (var i = criteriaListCount - 1; i >= 0; i--)
                                {
                                    var index = i;
                                    var criteria = criteriaList[i];

                                    var criteriaData = criteria.data as CVImageKeyFrameCriteriaData;

                                    // mark a request in progress inside the lock to avoid race conditions.. must be done before starting async process
                                    // mark that we are in progress by putting entries in the dictionary
                                    if (_requestTracker.TryGetValue(segmentNumber, out var reqValue))
                                    {
                                        reqValue[index] = null;
                                    }
                                    if (_resultTracker.TryGetValue(segmentNumber, out var resValue))
                                    {
                                        resValue[index] = null;
                                    }


                                    // do NOT await this, let it run async
                                    _ = CVServiceManager.GetInstance().PostCriteriaImageMatch(
                                        request: new CVImageCriteriaRequest()
                                        {
                                            screenshot = new CVImageBinaryData()
                                            {
                                                width = width,
                                                height = height,
                                                data = imageData
                                            },
                                            imageToMatch = criteriaData.imageData
                                        },
                                        abortRegistrationHook:
                                        action =>
                                        {
                                            RGDebug.LogVerbose($"CVImageCriteriaEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - abortHook registration callback");
                                            lock (_requestTracker)
                                            {
                                                RGDebug.LogVerbose($"CVImageCriteriaEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - abortHook registration callback - insideLock");

                                                if (_requestTracker.TryGetValue(segmentNumber, out var requestValue))
                                                {
                                                    // make sure we haven't already cleaned this up
                                                    if (requestValue.ContainsKey(index))
                                                    {
                                                        requestValue[index] = action;
                                                    }
                                                }
                                            }
                                        },
                                        onSuccess:
                                        list =>
                                        {
                                            RGDebug.LogVerbose($"CVImageCriteriaEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - onSuccess callback");
                                            lock (_requestTracker)
                                            {
                                                RGDebug.LogVerbose($"CVImageCriteriaEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - onSuccess callback - insideLock");
                                                // make sure we haven't already cleaned this up
                                                if (_resultTracker.TryGetValue(segmentNumber, out var value))
                                                {
                                                    RGDebug.LogVerbose($"CVImageCriteriaEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - onSuccess callback - storingResult");
                                                    // store the result
                                                    value[index] = list;
                                                    // cleanup the request tracker
                                                    var reqSeg = _requestTracker[segmentNumber];
                                                    reqSeg.Remove(index, out _);
                                                    // last one to come back.. cleanup all the way
                                                    if (reqSeg.Count == 0)
                                                    {
                                                        _priorResultsTracker.Remove(segmentNumber);
                                                        _requestTracker.Remove(segmentNumber);
                                                    }
                                                }
                                            }
                                        },
                                        onFailure:
                                        () =>
                                        {
                                            RGDebug.LogWarning($"CVImageCriteriaEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - onFailure callback - failure invoking CVService image criteria evaluation");
                                            lock (_requestTracker)
                                            {
                                                RGDebug.LogVerbose($"CVImageCriteriaEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - onFailure callback - insideLock");
                                                // make sure we haven't already cleaned this up
                                                if (_resultTracker.TryGetValue(segmentNumber, out var value))
                                                {
                                                    RGDebug.LogVerbose($"CVImageCriteriaEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - onFailure callback - storingResult");
                                                    // store the result as no result so we know we 'finished', but won't pass this criteria yet
                                                    value[index] = new();
                                                    // cleanup the request tracker
                                                    var reqSeg = _requestTracker[segmentNumber];
                                                    reqSeg.Remove(index, out _);
                                                    // last one to come back.. cleanup all the way
                                                    if (reqSeg.Count == 0)
                                                    {
                                                        _priorResultsTracker.Remove(segmentNumber);
                                                        _requestTracker.Remove(segmentNumber);
                                                    }
                                                }
                                            }

                                        });
                                    RGDebug.LogVerbose($"CVImageCriteriaEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - SENT");
                                    requestsInProgress = true;
                                }
                            }
                            else
                            {
                                resultList.Add("Awaiting screenshot data ...");
                            }
                        }
                    }
                }

                if (requestsInProgress)
                {
                    if (priorResults == null )
                    {
                        resultList.Add("Awaiting CV Image evaluation results ...");
                    }
                    else
                    {
                        // show the prior failures
                        resultList.AddRange(priorResults);
                    }
                }
            }

            RGDebug.LogVerbose($"CVImageCriteriaEvaluator - Matched - botSegment: {segmentNumber} - resultList: {resultList.Count} - END");

            return resultList;
        }
    }
}
