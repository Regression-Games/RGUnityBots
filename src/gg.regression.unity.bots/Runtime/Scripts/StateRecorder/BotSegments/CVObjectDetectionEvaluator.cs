using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.CVService;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments
{

    /**
     * <summary>Evaluates CV Image criteria using CVServiceManager to send/receive HTTP requests to a python server for doing the actual CV evaluations.
     * Python 'detects' a source image template in a provided screenshot and then this class evaluates those results against the specified bot segment criteria.</summary>
     */
    public static class CVObjectDetectionEvaluator
    {
        // This class uses explicit locking for thread safety as we have multiple different threads affecting the state of the tracking dictionaries, as well as async web responses
        // We manage locks by locking just on the _requestTracker for access to both dictionaries

        // if an entry is missing, no request for that segment in progress
        private static readonly Dictionary<int, ConcurrentDictionary<int, Action>> _requestTracker = new();

        // if an entry is NULL, request is in progress for that segment
        // if an entry has a value, then it is completed for that segment.. it should be cleared out on the next matched call if the result didn't match so it can run again
        private static readonly Dictionary<int, ConcurrentDictionary<int, List<CVObjectDetectionResult>>> _queryResultTracker = new();

        private static readonly Dictionary<int, List<string>> _priorResultsTracker = new();

        public static void Reset()
        {
            lock (_requestTracker)
            {
                foreach (var keyValuePair in _requestTracker.Where((pair => pair.Value != null)))
                {
                    RGDebug.LogDebug($"CVObjectDetectionEvaluator - Reset - botSegment: {keyValuePair.Key} - abortingWebRequest");
                    var pair = keyValuePair.Value;
                    foreach (var pairValue in pair.Values)
                    {
                        pairValue.Invoke();
                    }
                }

                _requestTracker.Clear();
                _queryResultTracker.Clear();
                _priorResultsTracker.Clear();
            }
        }


        // cleanup async and results tracking for that segment
        public static void Cleanup(int segmentNumber)
        {
            RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Cleanup - botSegment: {segmentNumber} - BEGIN");
            lock (_requestTracker)
            {
                RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Cleanup - botSegment: {segmentNumber} - insideLock");
                if (_requestTracker.Remove(segmentNumber, out var requests))
                {
                    try
                    {
                        RGDebug.LogDebug($"CVObjectDetectionEvaluator - Cleanup - botSegment: {segmentNumber} - abortingWebRequests");
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
                _queryResultTracker.Remove(segmentNumber, out _);
                _priorResultsTracker.Remove(segmentNumber, out _);
                RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Cleanup - botSegment: {segmentNumber} - END");
            }
        }

        // Returns a list of non-matched entries
        public static List<string> Matched(int segmentNumber, List<KeyFrameCriteria> criteriaList)
        {
            RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - BEGIN");
            var resultList = new List<string>();

            ConcurrentDictionary<int,List<CVObjectDetectionResult>> objectDetectionResults = null;
            List<string> priorResults = null;
            var requestInProgress = false;
            lock (_requestTracker)
            {
                requestInProgress = _requestTracker.ContainsKey(segmentNumber);
                _queryResultTracker.TryGetValue(segmentNumber, out objectDetectionResults);
                _priorResultsTracker.TryGetValue(segmentNumber, out priorResults);
                var resultsString = objectDetectionResults == null ? "null":$"\n[{string.Join(",\n", objectDetectionResults.Select(pair => $"{pair.Key}:[{(pair.Value==null?"null":string.Join(",\n", pair.Value))}]"))}]";
                if (objectDetectionResults != null)
                {
                    RGDebug.LogDebug($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - cvImageResults: {resultsString}");
                }
            }

            var criteriaListCount = criteriaList.Count;
            // only query when no request in progress and when we have cleared out the prior results
            if (!requestInProgress && objectDetectionResults == null)
            {
                var imageData = ScreenshotCapture.GetCurrentScreenshot(segmentNumber, out var width, out var height);
                if (imageData != null)
                {
                    // mark a request in progress inside the lock to avoid race conditions.. must be done before starting async process
                    // mark that we are in progress by putting entries in the dictionary of null until we replace with the real data
                    // thus contains key returns true
                    lock (_requestTracker)
                    {
                        _requestTracker[segmentNumber] = new();
                        _queryResultTracker[segmentNumber] = new();
                    }

                    for (var i = criteriaListCount - 1; i >= 0; i--)
                    {
                        var index = i;
                        var criteria = criteriaList[i];

                        var criteriaData = criteria.data as CVObjectDetectionKeyFrameCriteriaData;

                        // mark a request in progress inside the lock to avoid race conditions.. must be done before starting async process
                        // mark that we are in progress by putting entries in the dictionary
                        lock (_requestTracker)
                        {
                            if (_requestTracker.TryGetValue(segmentNumber, out var reqValue))
                            {
                                reqValue[index] = null;
                            }

                            if (_queryResultTracker.TryGetValue(segmentNumber, out var resValue))
                            {
                                resValue[index] = null;
                            }
                        }

                        RectInt? withinRect = null;

                        if (criteriaData.withinRect != null)
                        {
                            // compute the relative withinRect for the request
                            var xScale = width / criteriaData.withinRect.screenSize.x;
                            var yScale = height / criteriaData.withinRect.screenSize.y;
                            withinRect = new RectInt(
                                Mathf.FloorToInt(xScale * criteriaData.withinRect.rect.x),
                                Mathf.FloorToInt(yScale * criteriaData.withinRect.rect.y),
                                Mathf.FloorToInt(xScale * criteriaData.withinRect.rect.width),
                                Mathf.FloorToInt(yScale * criteriaData.withinRect.rect.height)
                                );
                        }

                        var queryText = criteriaData.text;
                        // do NOT await this, let it run async
                        _ = CVServiceManager.GetInstance().PostCriteriaObjectTextQuery(
                            new CVObjectDetectionRequest()
                            {
                                screenshot = new CVImageBinaryData()
                                {
                                    width = width,
                                    height = height,
                                    data = imageData
                                },
                                index = 1,
                                queryImage = null,
                                queryText = queryText
                            },
                            abortRegistrationHook:
                            action =>
                            {
                                RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - abortHook registration callback");
                                lock (_requestTracker)
                                {
                                    RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - abortHook registration callback - insideLock");

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
                                RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - onSuccess callback");
                                lock (_requestTracker)
                                {
                                    RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - onSuccess callback - insideLock");
                                    // make sure we haven't already cleaned this up
                                    if (_queryResultTracker.TryGetValue(segmentNumber, out var value))
                                    {
                                        RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - onSuccess callback - storingResult");
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
                                RGDebug.LogWarning($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - onFailure callback - failure invoking CVService image criteria evaluation");
                                lock (_requestTracker)
                                {
                                    RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - onFailure callback - insideLock");
                                    // make sure we haven't already cleaned this up
                                    if (_queryResultTracker.TryGetValue(segmentNumber, out var value))
                                    {
                                        RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - onFailure callback - storingResult");
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
                        RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - SENT");
                        resultList.Add("Awaiting CV Image evaluation results ...");
                    }
                }
                else
                {
                    resultList.Add("Awaiting screenshot data ...");
                }
            }
            else
            {
                // we check priorResults null here so that we only evaluate a new result 1 time
                if (!requestInProgress && (priorResults == null && objectDetectionResults != null && objectDetectionResults.Count == criteriaListCount && objectDetectionResults.All(cv => cv.Value != null)))
                {
                    // Evaluate the results if we have them all for each criteria
                    for (var i = criteriaListCount - 1; i >= 0; i--)
                    {
                        var criteria = criteriaList[i];
                        var found = false;
                        if (!criteria.transient || !criteria.Replay_TransientMatched)
                        {
                            var criteriaData = criteria.data as CVObjectDetectionKeyFrameCriteriaData;

                            if (objectDetectionResults.TryGetValue(i, out var cvImageResultList) && cvImageResultList is { Count: > 0 })
                            {
                                var withinRect = criteriaData.withinRect;
                                // we had the result for this criteria
                                if (withinRect != null)
                                {
                                    foreach (var cvImageResult in cvImageResultList)
                                    {
                                        // ensure result rect is inside
                                        var relativeScaling = new Vector2(withinRect.screenSize.x / (float)cvImageResult.resolution.x, withinRect.screenSize.y / (float)cvImageResult.resolution.y);

                                        // check the bottom left and top right to see if it intersects our rect
                                        var bottomLeft = new Vector2Int(Mathf.CeilToInt(cvImageResult.rect.x * relativeScaling.x), Mathf.CeilToInt(cvImageResult.rect.y * relativeScaling.y));
                                        var topRight = new Vector2Int(bottomLeft.x + Mathf.FloorToInt(cvImageResult.rect.width * relativeScaling.x), bottomLeft.y + Mathf.FloorToInt(cvImageResult.rect.height * relativeScaling.y));

                                        // we currently test overlap, should we test fully inside instead ??
                                        if (withinRect.rect.Contains(bottomLeft) || withinRect.rect.Contains(topRight))
                                        {
                                            found = true;
                                            break; // we found one, we can stop
                                        }
                                    }

                                    if (!found)
                                    {
                                        resultList.Add($"CV Image result for criteria at index: {i} was not found withinRect: {RectIntJsonConverter.ToJsonString(withinRect.rect)}");
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
                        // remove this so the next update pass knows to query again
                        _queryResultTracker.Remove(segmentNumber);
                    }
                }
                else
                {
                    resultList.Add("Awaiting CV Image evaluation results ...");
                }
            }

            lock (_requestTracker)
            {
                _priorResultsTracker[segmentNumber] = resultList;
            }

            RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - resultList: {resultList.Count} - END");

            return resultList;
        }
    }
}
