using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.BotSegments.Models.CVService;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using StateRecorder.BotSegments.Models;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    /**
     * <summary>Data for clicking on or moving the mouse to a CV Text location in the scene</summary>
     */
    [Serializable]
    public class CVTextMouseActionData : IBotActionData
    {
        // api version for this object, update if object format changes
        public int apiVersion = SdkApiVersion.VERSION_12;

        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.Mouse_CVText;

        public string text;
        public TextMatchingRule textMatchingRule = TextMatchingRule.Matches;
        public TextCaseRule textCaseRule = TextCaseRule.Matches;

        /**
         * optionally limit the rect area where the TextData can be detected
         */
        [CanBeNull]
        public CVWithinRect withinRect;

        /**
         * the list of actions to perform at this location
         * Note: This does NOT re-evaluate the position of the CVText on each action.
         * This is modeled as a list to allow you to click and/or un-click buttons on this Text location without re-evaluating the CV text match in between if you don't want to.
         * If you want to validate a mouse visual effect in your criteria, and then mouse up in the next bot segment; that is also a normal way to use this system with just 1 list action per segment.
         *
         * If you want to perform click and drag mouse movements, you cannot and should not try to do that with this list.  You need to create 2 separate bot segments.
         * Mouse down in one bot segment, then mouse up as a separate bot segment.
         */
        public List<CVMouseActionDetails> actions;

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
            // get the CV evaluate started...
            RequestCVTextEvaluation(segmentNumber);
        }

        private void RequestCVTextEvaluation(int segmentNumber)
        {
            lock (_syncLock)
            {
                if (!_requestInProgress)
                {
                    // Request the CV Text data
                    var screenshot = ScreenshotCapture.GetCurrentScreenshot(segmentNumber, out var width, out var height);
                    if (screenshot != null)
                    {
                        _requestInProgress = true;
                        _resultReceived = false;
                        _cvResultsBoundsRect = null;

                        // do NOT await this, let it run async
                        _ = CVServiceManager.GetInstance().PostCriteriaTextDiscover(
                            request: new CVTextCriteriaRequest()
                            {
                                screenshot = new CVImageBinaryData()
                                {
                                    width = width,
                                    height = height,
                                    data = screenshot
                                }
                            },
                            abortRegistrationHook:
                            action =>
                            {
                                RGDebug.LogVerbose($"CVTextMouseActionData - RequestCVTextEvaluation - botSegment: {segmentNumber} - Request - abortHook registration callback");
                                lock (_syncLock)
                                {
                                    RGDebug.LogVerbose($"CVTextMouseActionData - RequestCVTextEvaluation - botSegment: {segmentNumber} - abortHook registration callback - insideLock");
                                    _requestAbortAction = action;
                                }
                            },
                            onSuccess:
                            cvTextResults =>
                            {
                                RGDebug.LogDebug($"CVTextMouseActionData - RequestCVTextEvaluation - botSegment: {segmentNumber} - Request - onSuccess callback");
                                lock (_syncLock)
                                {
                                    RGDebug.LogVerbose($"CVTextMouseActionData - RequestCVTextEvaluation - botSegment: {segmentNumber} - Request - onSuccess callback - insideLock");
                                    // make sure we haven't already cleaned this up
                                    if (_requestAbortAction != null)
                                    {
                                        RGDebug.LogVerbose($"CVTextMouseActionData - RequestCVTextEvaluation - botSegment: {segmentNumber} - Request - onSuccess callback - storingResult");

                                        // pick a random rect from the results, if they didn't want this random, they should have specified within rect
                                        if (cvTextResults.Count > 0)
                                        {
                                            var textParts = text.Trim().Split(' ').Select(a => a.Trim()).Where(a => a.Length > 0).ToList();

                                            //  TODO: Find the possible bounding boxes that contain all the words... pick the smallest , or the one that is 'within rect'



                                            var cvTextResultsCount = cvTextResults.Count;
                                            for (var k = 0; k < cvTextResultsCount && textParts.Count > 0; k++)
                                            {
                                                var cvTextResult = cvTextResults[k];
                                                var cvText = cvTextResult.text.Trim();

                                                if (textCaseRule == TextCaseRule.Ignore)
                                                {
                                                    cvText = cvText.ToLower();
                                                }

                                                for (var j = textParts.Count - 1; j >= 0; j--)
                                                {
                                                    var matched = false;
                                                    var textToMatch = textParts[j];

                                                    if (textCaseRule == TextCaseRule.Ignore)
                                                    {
                                                        textToMatch = textToMatch.ToLower();
                                                    }

                                                    switch (textMatchingRule)
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
                                                        if (withinRect == null)
                                                        {
                                                            textParts.RemoveAt(j); // remove the one we matched so we don't have to check it again
                                                            break;
                                                        }
                                                        else
                                                        {
                                                            // ensure result rect is inside
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
                                                _lastError = $"Missing CVText - text: {text.Trim()}, caseRule: {textCaseRule}, matchRule: {textMatchingRule}, missingWords: [{string.Join(',',textParts)}]";
                                            }
                                            else
                                            {
                                                _lastError = null;
                                            }

                                            _cvResultsBoundsRect = TODO;
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
                            },
                            onFailure:
                            () =>
                            {
                                RGDebug.LogWarning($"CVTextMouseActionData - RequestCVTextEvaluation - botSegment: {segmentNumber} - Request - onFailure callback - failure invoking CVService Text criteria evaluation");
                                lock (_syncLock)
                                {
                                    RGDebug.LogVerbose($"CVTextMouseActionData - RequestCVTextEvaluation - botSegment: {segmentNumber} - Request - onFailure callback - insideLock");
                                    // make sure we haven't already cleaned this up
                                    if (_requestAbortAction != null)
                                    {
                                        RGDebug.LogVerbose($"CVTextMouseActionData - RequestCVTextEvaluation - botSegment: {segmentNumber} - Request - onFailure callback - storingResult");
                                        _resultReceived = true;
                                        _cvResultsBoundsRect = null;

                                        // cleanup the request tracker
                                        _requestAbortAction = null;
                                        _requestInProgress = false;
                                        _nextActionIndex = 0;
                                    }
                                }

                            });
                        RGDebug.LogDebug($"CVTextMouseActionData - RequestCVTextEvaluation - botSegment: {segmentNumber} - Request - SENT");
                    }
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
                // see if we're ready to evaluate the next action; this is done BEFORE the count check so the last element in the action list still enforces its runtime
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
                                    RGDebug.LogDebug($"CVTextMouseActionData - ProcessAction - botSegment: {segmentNumber} - frame: {Time.frameCount} - Sending Raw Position Mouse Event: {currentAction} at position: {VectorIntJsonConverter.ToJsonString(position)}");
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
                                    RGDebug.LogDebug($"CVTextMouseActionData - ProcessAction - botSegment: {segmentNumber} - frame: {Time.frameCount} - TextData not found in current screen ...");
                                    _lastError = "CVTextMouseActionData - TextData not found in current screen ...";
                                    error = _lastError;
                                    // start a new request
                                    RequestCVTextEvaluation(segmentNumber);
                                    return false;
                                }
                            }
                            else
                            {
                                RGDebug.LogDebug($"CVTextMouseActionData - ProcessAction - botSegment: {segmentNumber} - frame: {Time.frameCount} - waiting for CV Text evaluation results ...");
                                _lastError = "CVTextMouseActionData - waiting for CV Text evaluation results ...";
                                error = _lastError;
                                // make sure we have a request in progress (this call checks internally to make sure one isn't already in progress)
                                RequestCVTextEvaluation(segmentNumber);
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
            // for cv Text analysis, we finish the action queue even if criteria match before hand... don't set _isStopped;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"text\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, text);
            stringBuilder.Append(",\"textCaseRule\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, textCaseRule.ToString());
            stringBuilder.Append(",\"textMatchingRule\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, textMatchingRule.ToString());
            stringBuilder.Append(",\"withinRect\":");
            CVWithinRectJsonConverter.WriteToStringBuilderNullable(stringBuilder, withinRect);
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
