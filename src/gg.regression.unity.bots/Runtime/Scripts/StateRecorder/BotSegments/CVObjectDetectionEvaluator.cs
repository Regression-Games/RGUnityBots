using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.CVService;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments
{


    /**
     * <summary>Evaluates CV Text criteria using CVServiceManager to send/receive HTTP requests to a python server for doing the actual CV evaluations.
     * Python 'detects' text in a provided image and then this class evaluates those results against the specified bot segment criteria.</summary>
     */
    public static class CVObjectDetectionEvaluator
    {
        // This class uses explicit locking for thread safety as we have multiple different threads affecting the state of the tracking dictionaries, as well as async web responses
        // We manage locks by locking just on the _requestTracker for access to both dictionaries

        // if an entry is missing, no request for that segment in progress
        private static readonly Dictionary<int, Action> _requestTracker = new();

        // if an entry is NULL, request is in progress for that segment
        // if an entry has a value, then it is completed for that segment.. it should be cleared out on the next matched call if the result didn't match so it can run again
        private static readonly Dictionary<int, List<CVObjectDetectionResult>> _resultTracker = new();

        private static readonly Dictionary<int, List<string>> _priorResultsTracker = new();

        public static void Reset()
        {
            lock (_requestTracker)
            {
                foreach (var keyValuePair in _requestTracker.Where((pair => pair.Value != null)))
                {
                    RGDebug.LogDebug($"CVObjectDetectionEvaluator - Reset - botSegment: {keyValuePair.Key} - abortingWebRequest");
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
            RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Cleanup - botSegment: {segmentNumber} - BEGIN");
            lock (_requestTracker)
            {
                RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Cleanup - botSegment: {segmentNumber} - insideLock");
                if (_requestTracker.Remove(segmentNumber, out var request))
                {
                    try
                    {
                        RGDebug.LogDebug($"CVObjectDetectionEvaluator - Cleanup - botSegment: {segmentNumber} - abortingWebRequest");
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
                RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Cleanup - botSegment: {segmentNumber} - END");
            }
        }

        // Returns a list of non-matched entries
        public static List<string> Matched(int segmentNumber, List<KeyFrameCriteria> criteriaList)
        {
            RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - BEGIN");
            var resultList = new List<string>();

            List<CVObjectDetectionResult> cvObjectDetectionResults = null;
            List<string> priorResults = null;
            lock (_requestTracker)
            {
                _resultTracker.TryGetValue(segmentNumber, out cvObjectDetectionResults);
                _priorResultsTracker.TryGetValue(segmentNumber, out priorResults);
                var resultsString = cvObjectDetectionResults == null ? "null":$"\n[{string.Join(",\n", cvObjectDetectionResults)}]";
                if (cvObjectDetectionResults != null)
                {
                    RGDebug.LogDebug($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - CVObjectDetectionResults: {resultsString}");
                }
            }
            
            if (priorResults is not { Count: 0 } || cvObjectDetectionResults == null)
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
                            foreach (var criteria in criteriaList)
                            {
                                
                                var criteriaData = criteria.data as CVObjectDetectionKeyFrameCriteriaData;
                                var imageData = ScreenshotCapture.GetCurrentScreenshot(
                                    segmentNumber,
                                    out var width,
                                    out var height);
                                var queryText = criteriaData.text;
                                if (imageData != null)
                                {
                                    // mark a request in progress inside the lock to avoid race conditions.. must be done before starting async process
                                    // mark that we are in progress by putting entries in the dictionary of null until we replace with the real data
                                    // thus contains key returns true
                                    _requestTracker[segmentNumber] = null;
                                    _resultTracker[segmentNumber] = null;

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
                                        abortRegistrationHook: action =>
                                        {
                                            RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - Request - abortHook registration callback");
                                            lock (_requestTracker)
                                            {
                                                RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - Request - abortHook registration callback - insideLock");
                                                // make sure we haven't already cleaned this up
                                                if (_requestTracker.ContainsKey(segmentNumber))
                                                {
                                                    _requestTracker[segmentNumber] = action;
                                                }
                                            }
                                        },
                                        onSuccess: list =>
                                        {
                                            RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - Request - onSuccess callback");
                                            lock (_requestTracker)
                                            {
                                                RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - Request - onSuccess callback - insideLock");
                                                // make sure we haven't already cleaned this up
                                                if (_resultTracker.ContainsKey(segmentNumber))
                                                {
                                                    RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - Request - onSuccess callback - storingResult");
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
                                            RGDebug.LogWarning($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - Request - onFailure callback - failure invoking CVService text criteria evaluation");
                                            lock (_requestTracker)
                                            {
                                                RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - Request - onFailure callback - insideLock");
                                                // make sure we haven't already cleaned this up
                                                if (_resultTracker.ContainsKey(segmentNumber))
                                                {
                                                    RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - Request - onFailure callback - storingResult");
                                                    // store the result as empty so we know we finished, but won't pass
                                                    _resultTracker[segmentNumber] = new();
                                                    // cleanup the request tracker
                                                    _requestTracker.Remove(segmentNumber);
                                                    _priorResultsTracker.Remove(segmentNumber);
                                                }
                                            }
                                        });
                                    RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - Request - SENT");
                                    requestInProgress = true;
                                }
                                else
                                {
                                    resultList.Add("Awaiting screenshot data ...");
                                }
                            }
                        }
                    }
                }

                if (requestInProgress)
                {
                    if (priorResults == null )
                    {
                        resultList.Add("Awaiting CV object detection evaluation result ...");
                    }
                    else
                    {
                        // show the prior failures
                        resultList.AddRange(priorResults);
                    }
                }
            }

            RGDebug.LogVerbose($"CVObjectDetectionEvaluator - Matched - botSegment: {segmentNumber} - resultList: {resultList.Count} - END");

            return resultList;
        }
    }
}
