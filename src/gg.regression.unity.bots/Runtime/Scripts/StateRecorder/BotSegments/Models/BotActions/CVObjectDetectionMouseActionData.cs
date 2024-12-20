﻿using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.BotSegments.Models.AIService;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RegressionGames.StateRecorder.BotSegments.Models.BotActions
{
    /**
     * <summary>Data for clicking on or moving the mouse to a CV Object Detection location in the scene</summary>
     */
    [Serializable]
    public class CVObjectDetectionMouseActionData : IBotActionData
    {
        // api version for this object, update if object format changes
        public int apiVersion = SdkApiVersion.VERSION_19;

        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.Mouse_ObjectDetection;

        /**
         * base64 encoded byte[] of jpg image data , NOT the raw pixel data, the full jpg file bytes
         */
        [CanBeNull]
        public string imageQuery;

        /**
         * The text query to be used for object detection
         */
        [CanBeNull]
        public string textQuery;

        /**
         * optionally limit the rect area where the imageQuery or textQuery can be detected
         */
        [CanBeNull]
        public CVWithinRect withinRect;

        /**
         * the list of actions to perform at this location
         * Note: This does NOT re-evaluate the position of the CVImage on each action.
         * This is modeled as a list to allow you to click and/or un-click buttons on this image location without re-evaluating the CV image match in between if you don't want to.
         * If you want to validate a mouse visual effect in your criteria, and then mouse up in the next bot segment; that is also a normal way to use this system with just 1 list action per segment.
         *
         * If you want to perform click and drag mouse movements, you cannot and should not try to do that with this list.  You need to create 2 separate bot segments.
         * Mouse down in one bot segment, then mouse up as a separate bot segment.
         */
        public List<CVMouseActionDetails> actions;

        /**
         * Optional threshold to accept a returned match from the object detection model. Returned matches with a confidence score less than this threshold are ignored.
         */
        public float? threshold;

        private bool _isStopped;

        private float _nextActionTime = float.MinValue;
        private int _nextActionIndex = 0;

        private bool _resultReceived = false;
        private RectInt? _cvResultsBoundsRect = null;

        // if an entry is missing, no request for that segment in progress
        private bool _requestInProgress = false;
        private Action _requestAbortAction = null;
        private readonly object _syncLock = new();

        private string _lastError = null;

        public bool IsCompleted()
        {
            return _isStopped;
        }

        public void ReplayReset()
        {
            _isStopped = false;
            _nextActionTime = float.MinValue;
            _nextActionIndex = 0;
            _resultReceived = false;
            _cvResultsBoundsRect = null;
            lock (_syncLock)
            {
                if (_requestAbortAction != null)
                {
                    _requestAbortAction.Invoke();
                    _requestAbortAction = null;
                }

                _requestInProgress = false;
            }
        }

        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            if (!_isStopped)
            {
                // get the CV evaluate started...
                RequestCVObjectDetectionEvaluation(segmentNumber);
            }
        }

        private void RequestCVObjectDetectionEvaluation(int segmentNumber)
        {
            lock (_syncLock)
            {
                if (!_requestInProgress)
                {
                    // Request the CV Image data
                    var screenshot = ScreenshotCapture.GetInstance().GetCurrentScreenshot(segmentNumber, out var width, out var height);
                    if (screenshot != null)
                    {
                        _requestInProgress = true;
                        RectInt? queryWithinRect = null;
                        if (withinRect != null)
                        {
                            // compute the relative withinRect for the request
                            var xScale = width / withinRect.screenSize.x;
                            var yScale = height / withinRect.screenSize.y;
                            queryWithinRect = new RectInt(
                                Mathf.FloorToInt(xScale * withinRect.rect.x),
                                Mathf.FloorToInt(yScale * withinRect.rect.y),
                                Mathf.FloorToInt(xScale * withinRect.rect.width),
                                Mathf.FloorToInt(yScale * withinRect.rect.height)
                            );
                        }

                        _resultReceived = false;
                        _cvResultsBoundsRect = null;

                        // do NOT await this, let it run async
                        _ = AIServiceManager.GetInstance().PostCriteriaObjectDetection(
                            new CVObjectDetectionRequest(
                                screenshot: new CVImageBinaryData()
                                {
                                    width = width,
                                    height = height,
                                    data = screenshot
                                },
                                textQuery: textQuery,
                                imageQuery: CVImageCriteriaEvaluator.GetImageData(imageQuery),
                                withinRect: queryWithinRect,
                                threshold: threshold
                            ),
                            // Register the abort action.
                            abortRegistrationHook: action => OnAbort(segmentNumber, action),

                            // Extracts the results, select a random result if multiple are returned, and clean up request tracker.
                            onSuccess: list => OnSuccess(segmentNumber, list),

                            // Log the failure, reset relevant state variables, and clean up resources.
                            onFailure: () => OnFailure(segmentNumber)
                        );
                        RGDebug.LogDebug($"CVObjectDetectionMouseActionData - RequestCVObjectDetectionEvaluation - botSegment: {segmentNumber} - Request - SENT");
                    }
                }
            }
        }

        /// <summary>
        /// Registers an abort action for the CV image evaluation request.
        /// </summary>
        /// <param name="segmentNumber">The segment number of the bot action.</param>
        /// <param name="action">The abort action to be registered.</param>
        private void OnAbort(int segmentNumber, Action action)
        {
            RGDebug.LogVerbose($"CVObjectDetectionMouseActionData - RequestCVObjectDetectionEvaluation - botSegment: {segmentNumber} - Request - abortHook registration callback");
            lock (_syncLock)
            {
                RGDebug.LogVerbose($"CVObjectDetectionMouseActionData - RequestCVObjectDetectionEvaluation - botSegment: {segmentNumber} - abortHook registration callback - insideLock");
                _requestAbortAction = action;
            }
        }

        /// <summary>
        /// Handles the failure of a CV image evaluation request.
        /// This method logs the failure, resets relevant state variables, and cleans up resources.
        /// It ensures that the system is ready for potential retry attempts or further actions.
        /// </summary>
        /// <param name="segmentNumber">The segment number of the bot action, used for logging and debugging purposes.</param>
        private void OnFailure(int segmentNumber)
        {
            RGDebug.LogWarning($"CVObjectDetectionMouseActionData - RequestCVObjectDetectionEvaluation - botSegment: {segmentNumber} - Request - onFailure callback - failure invoking AIService criteria-object-text-query evaluation");
            lock (_syncLock)
            {
                RGDebug.LogVerbose($"CVObjectDetectionMouseActionData - RequestCVObjectDetectionEvaluation - botSegment: {segmentNumber} - Request - onFailure callback - insideLock");
                // make sure we haven't already cleaned this up
                if (_requestAbortAction != null)
                {
                    RGDebug.LogVerbose($"CVObjectDetectionMouseActionData - RequestCVObjectDetectionEvaluation - botSegment: {segmentNumber} - Request - onFailure callback - storingResult");
                    _resultReceived = true;
                    _cvResultsBoundsRect = null;

                    // cleanup the request tracker
                    _requestAbortAction = null;
                    _requestInProgress = false;
                    _nextActionIndex = 0;
                }
            }
        }

        /// <summary>
        /// Handles the successful completion of a CV image evaluation request.
        /// This method extracts the results, selects a random result if multiple are returned,
        /// and cleans up request tracker.
        /// </summary>
        /// <param name="segmentNumber">The segment number of the bot action, used for logging and debugging purposes.</param>
        /// <param name="list">A list of CVObjectDetectionResult objects returned from the CV evaluation.</param>
        private void OnSuccess(int segmentNumber, List<CVImageResult> list)
        {
            RGDebug.LogDebug($"CVObjectDetectionMouseActionData - RequestCVObjectDetectionEvaluation - botSegment: {segmentNumber} - Request - onSuccess callback");
            lock (_syncLock)
            {
                RGDebug.LogVerbose($"CVObjectDetectionMouseActionData - RequestCVObjectDetectionEvaluation - botSegment: {segmentNumber} - Request - onSuccess callback - insideLock");
                // make sure we haven't already cleaned this up
                if (_requestAbortAction != null)
                {
                    RGDebug.LogVerbose($"CVObjectDetectionMouseActionData - RequestCVObjectDetectionEvaluation - botSegment: {segmentNumber} - Request - onSuccess callback - storingResult");

                    // pick a random rect from the results, if they didn't want this random, they should have specified within rect
                    if (list.Count > 1)
                    {
                        RGDebug.LogInfo($"({segmentNumber}) CVObjectDetectionMouseActionData - Multiple results were returned for CV Image evaluation.  A random one of these will be saved as the result.  Consider specifying a precise `withinRect` in your action definition to get a singular result.");
                    }

                    if (list.Count > 0)
                    {
                        var index = Random.Range(0, list.Count);

                        _cvResultsBoundsRect = list[index].rect;
                    }
                    else
                    {
                        _cvResultsBoundsRect = null;
                    }

                    _resultReceived = true;

                    // cleanup the request tracker
                    _requestAbortAction = null;
                    _requestInProgress = false;
                }
            }
        }

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            if (actions.Count == 0)
            {
                _isStopped = true;
            }

            if (!_isStopped)
            {
                var now = Time.unscaledTime;
                // See if we're ready to evaluate the next action; this is done BEFORE the count check so the last element in the action list still enforces its runtime
                if (now - _nextActionTime > 0)
                {
                    if (_nextActionIndex < actions.Count)
                    {
                        lock (_syncLock)
                        {
                            if (_resultReceived)
                            {
                                if (_cvResultsBoundsRect.HasValue)
                                {
                                    var currentAction = actions[_nextActionIndex];

                                    // setup the next action after this one
                                    ++_nextActionIndex;
                                    _nextActionTime = now + currentAction.duration;

                                    var rect = _cvResultsBoundsRect.Value;

                                    var position = new Vector2Int((int)rect.center.x, (int)rect.center.y);
                                    RGDebug.LogDebug($"CVObjectDetectionMouseActionData - ProcessAction - botSegment: {segmentNumber} - frame: {Time.frameCount} - Sending Raw Position Mouse Event: {currentAction} at position: {Vector2IntJsonConverter.ToJsonString(position)}");
                                    MouseEventSender.SendRawPositionMouseEvent(
                                        replaySegment: segmentNumber,
                                        normalizedPosition: position,
                                        leftButton: currentAction.leftButton,
                                        middleButton: currentAction.middleButton,
                                        rightButton: currentAction.rightButton,
                                        forwardButton: currentAction.forwardButton,
                                        backButton: currentAction.backButton,
                                        scroll: currentAction.scroll
                                    );
                                    _lastError = null;
                                    error = _lastError;
                                    return true;
                                }
                                else
                                {
                                    RGDebug.LogDebug($"CVObjectDetectionMouseActionData - ProcessAction - botSegment: {segmentNumber} - frame: {Time.frameCount} - imageData not found in current screen ...");
                                    _lastError = "CVObjectDetectionMouseActionData - imageData not found in current screen ...";
                                    error = _lastError;
                                    // start a new request
                                    RequestCVObjectDetectionEvaluation(segmentNumber);
                                    return false;
                                }
                            }
                            else
                            {
                                RGDebug.LogDebug($"CVObjectDetectionMouseActionData - ProcessAction - botSegment: {segmentNumber} - frame: {Time.frameCount} - waiting for CV Object Detection results ...");
                                _lastError = "CVObjectDetectionMouseActionData - waiting for CV Object Detection results ...";
                                error = _lastError;
                                // make sure we have a request in progress (this call checks internally to make sure one isn't already in progress)
                                RequestCVObjectDetectionEvaluation(segmentNumber);
                                return false;
                            }
                        }
                    }
                    else
                    {
                        _isStopped = true;
                    }
                }
            }

            lock (_syncLock)
            {
                error = _lastError;
            }
            return false;
        }

        public void AbortAction(int segmentNumber)
        {
            // hard stop NOW
            _isStopped = true;
        }

        public void StopAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            // for cv object detection, we finish the action queue even if criteria match before hand... don't set _isStopped;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"imageData\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, imageQuery);
            stringBuilder.Append(",\"textQuery\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, textQuery);
            stringBuilder.Append(",\"withinRect\":");
            CVWithinRectJsonConverter.WriteToStringBuilderNullable(stringBuilder, withinRect);
            stringBuilder.Append(",\"threshold\":");
            FloatJsonConverter.WriteToStringBuilderNullable(stringBuilder, threshold);
            stringBuilder.Append(",\"actions\":[");
            var actionsCount = actions.Count;
            for (var i = 0; i < actionsCount; i++)
            {
                actions[i].WriteToStringBuilder(stringBuilder);
                if (i + 1 < actionsCount)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("]}");
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
