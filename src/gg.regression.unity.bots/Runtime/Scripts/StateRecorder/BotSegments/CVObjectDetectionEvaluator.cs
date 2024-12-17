using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.AIService;
using RegressionGames.StateRecorder.BotSegments.Models.BotCriteria;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments
{

    /**
     * <summary>Evaluates CV Object Detection criteria using AIServiceManager to send/receive HTTP requests to a python server for doing the actual CV evaluations.
     * Python 'detects' classes of objects in a provided screenshot based on image and text queries, and then this class evaluates those results against the specified bot segment criteria.</summary>
     */
    public static class CVObjectDetectionEvaluator
    {
        // This class uses explicit locking for thread safety as we have multiple different threads affecting the state of the tracking dictionaries, as well as async web responses
        // We manage locks by locking just on the _requestTracker for access to both dictionaries

        // if an entry is missing, no request for that segment in progress
        private static readonly Dictionary<int, ConcurrentDictionary<int, Action>> _requestTracker = new();

        // if an entry is NULL, request is in progress for that segment
        // if an entry has a value, then it is completed for that segment.. it should be cleared out on the next matched call if the result didn't match so it can run again
        private static readonly Dictionary<int, ConcurrentDictionary<int, List<CVImageResult>>> _queryResultTracker = new();

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

            ConcurrentDictionary<int, List<CVImageResult>> objectDetectionResults = null;
            List<string> priorResults = null;
            bool requestInProgress = false;

            // Try to get the results or request in progress.
            lock (_requestTracker)
            {
                // Check if we have results or a request in progress.
                requestInProgress = _requestTracker.ContainsKey(segmentNumber);

                // TODO(REG-1928): Think more about hardening this data reading.

                // Get the results for the segment.
                _queryResultTracker.TryGetValue(segmentNumber, out objectDetectionResults);
                // Get the prior results for the segment.
                _priorResultsTracker.TryGetValue(segmentNumber, out priorResults);

                // Print the results if there are any.
                if (objectDetectionResults != null)
                {
                    var resultsString = $"\n[{string.Join(",\n", objectDetectionResults.Select(pair => $"{pair.Key}:[{(pair.Value == null ? "null" : string.Join(",\n", pair.Value))}]"))}]";
                    RGDebug.LogDebug($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - cvImageResults: {resultsString}");
                }
            }

            var resultList = new List<string>();

            // If no results and no request, we'll initiate a new request.
            if (!requestInProgress && objectDetectionResults == null)
            {
                resultList = TryPostRequest(segmentNumber, criteriaList, resultList);
            }
            // If we have results and no request in progress, we'll evaluate them.
            // We check priorResults null here so that we only evaluate a new result 1 time.
            else if (!requestInProgress && objectDetectionResults != null && priorResults == null)
            {
                resultList = EvaluateResult(segmentNumber, criteriaList, objectDetectionResults, resultList);
            }
            // If a request is in progress, we'll wait for the results.
            else
            {
                resultList.Add("Awaiting CV Object Detection evaluation results ...");
            }

            lock (_requestTracker)
            {
                _priorResultsTracker[segmentNumber] = resultList;
            }
            RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - resultList: {resultList.Count} - END");

            return resultList;
        }

        /// <summary>
        /// Matches the given criteria list against the object detection results for a specific segment.
        /// </summary>
        /// <param name="segmentNumber">The number of the segment being evaluated.</param>
        /// <param name="criteriaList">The list of criteria to match against.</param>
        /// <param name="priorResults">The list of prior results for the segment.</param>
        /// <param name="objectDetectionResults">The list of object detection results for the segment.</param>
        /// <param name="resultList">The list of result strings to be updated.</param>
        /// <returns>A list of non-matched entries as strings.</returns>
        /// <remarks>
        /// This method performs the following steps:
        /// 1. Attempts to retrieve existing results for the given segment.
        /// 2. If no request is in progress and no results exist, it initiates a new request.
        /// 3. If results are available and no request is in progress, it evaluates the results against the criteria.
        /// 4. Updates the prior results tracker with the current result list.
        /// </remarks>
        private static List<string> EvaluateResult(
            int segmentNumber,
            List<KeyFrameCriteria> criteriaList,
            ConcurrentDictionary<int, List<CVImageResult>> objectDetectionResults,
            List<string> resultList)
        {
            int criteriaListCount = criteriaList.Count;
            bool allResultsHaveValue = objectDetectionResults.All(r => r.Value != null);
            if ((objectDetectionResults.Count == criteriaListCount && allResultsHaveValue))
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
                                found = DidMatchInsideWithinRect(cvImageResultList, withinRect);
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
                resultList.Add("Awaiting CV Object Detection evaluation results ...");
            }
            return resultList;
        }


        /// <summary>
        /// Checks if any of the CV object detection results match within the specified rectangle.
        /// </summary>
        /// <param name="cvImageResultList">List of CV detection results to check.</param>
        /// <param name="withinRect">The rectangle constraint to check against.</param>
        /// <returns>True if a match is found within the specified rectangle, otherwise false.</returns>
        /// <remarks>
        /// This method scales the detection results to match the withinRect's screen size,
        /// then checks if the shape overlaps the withinRect in any way. It stops checking after finding
        /// the first match.
        /// </remarks>
        public static bool DidMatchInsideWithinRect(List<CVImageResult> cvImageResultList, CVWithinRect withinRect)
        {
            bool found = false;
            foreach (var cvImageResult in cvImageResultList)
            {
                var relativeScaling = new Vector2(withinRect.screenSize.x / (float)cvImageResult.resolution.x, withinRect.screenSize.y / (float)cvImageResult.resolution.y);

                var minX = Mathf.CeilToInt(cvImageResult.rect.x * relativeScaling.x);
                var maxX = Mathf.FloorToInt((cvImageResult.rect.x + cvImageResult.rect.width) * relativeScaling.x);

                var minY = Mathf.CeilToInt(cvImageResult.rect.y * relativeScaling.y);
                var maxY = Mathf.FloorToInt((cvImageResult.rect.y + cvImageResult.rect.height) * relativeScaling.y);

                // we test that the shapes overlap, as long as the result in some way overlaps the withinRect, it passes
                if (withinRect.rect.xMin <= maxX && withinRect.rect.xMax >= minX && withinRect.rect.yMin <= maxY && withinRect.rect.yMax >= minY )
                {
                    found = true;
                    break; // we found one, we can stop
                }
            }
            return found;
        }


        /// <summary>
        /// Attempts to post a CV object detection request for each criteria in the given list.
        /// </summary>
        /// <param name="segmentNumber">The number of the segment being evaluated.</param>
        /// <param name="criteriaList">The list of key frame criteria to evaluate.</param>
        /// <param name="resultList">The list to store evaluation results.</param>
        /// <returns>The updated result list.</returns>
        /// <remarks>
        /// This method captures a screenshot, initializes request trackers, and posts CV object detection requests for each criteria.
        /// It handles the creation of within-rect constraints and sets up asynchronous request processing.
        /// The method ensures thread-safety by using locks on shared resources.
        /// </remarks>
        private static List<string> TryPostRequest(int segmentNumber, List<KeyFrameCriteria> criteriaList, List<string> resultList)
        {
            int criteriaListCount = criteriaList.Count;
            var imageData = ScreenshotCapture.GetInstance().GetCurrentScreenshot(segmentNumber, out var width, out var height);
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

                    var textQuery = criteriaData.textQuery;
                    var imageQuery = CVImageCriteriaEvaluator.GetImageData(criteriaData.imageQuery);
                    // do NOT await this, let it run async
                    _ = AIServiceManager.GetInstance().PostCriteriaObjectDetection(
                        new CVObjectDetectionRequest(
                            screenshot: new CVImageBinaryData()
                            {
                                width = width,
                                height = height,
                                data = imageData
                            },
                            textQuery: textQuery,
                            imageQuery: imageQuery,
                            withinRect: withinRect,
                            threshold: criteriaData.threshold,
                            index: index
                        ),
                        // Cancel ongoing request in a thread safe manner.
                        abortRegistrationHook: action => AbortRegistrationHook(segmentNumber, index, action),

                        // Stores the results, clean up the request tracker, and remove completed requests.
                        onSuccess: list => OnSuccess(segmentNumber, index, list),

                        // Logs the failure, store an empty result, clean up the request tracker, and removes completed request
                        onFailure: () => OnFailure(segmentNumber, index)
                    );
                    RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - SENT");
                    resultList.Add("Awaiting CV Object Detection evaluation results ...");
                }
            }
            else
            {
                resultList.Add("Awaiting screenshot data ...");
            }
            return resultList;
        }

        /// <summary>
        /// Registers an abort action for a specific segment and index in the request tracker.
        /// </summary>
        /// <param name="segmentNumber">The number of the segment being evaluated.</param>
        /// <param name="index">The index of the criteria within the segment.</param>
        /// <param name="action">The abort action to be registered.</param>
        /// <remarks>
        /// This method is used to register an abort action that can be invoked to cancel an ongoing request.
        /// It ensures thread-safety by using a lock on the _requestTracker.
        /// </remarks>
        private static void AbortRegistrationHook(int segmentNumber, int index, Action action)
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
        }

        /// <summary>
        /// Handles the successful completion of a CV object detection request.
        /// </summary>
        /// <param name="segmentNumber">The number of the segment being evaluated.</param>
        /// <param name="index">The index of the criteria within the segment.</param>
        /// <param name="list">The list of CV object detection results.</param>
        /// <remarks>
        /// This method is called when a CV object detection request completes successfully.
        /// It stores the results, cleans up the request tracker, and removes completed requests.
        /// The method ensures thread-safety by using a lock on the _requestTracker.
        /// </remarks>
        private static void OnSuccess(int segmentNumber, int index, List<CVImageResult> list)
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
        }

        /// <summary>
        /// Handles the failure of a CV object detection request.
        /// </summary>
        /// <param name="segmentNumber">The number of the segment being evaluated.</param>
        /// <param name="index">The index of the criteria within the segment.</param>
        /// <remarks>
        /// This method is called when a CV object detection request fails.
        /// It logs the failure, stores an empty result, cleans up the request tracker,
        /// and removes completed requests. The method ensures thread-safety by using
        /// a lock on the _requestTracker.
        /// </remarks>
        private static void OnFailure(int segmentNumber, int index)
        {
            RGDebug.LogWarning($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber}, index: {index} - Request - onFailure callback - failure invoking AIService image criteria evaluation");
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
        }


    }
}
