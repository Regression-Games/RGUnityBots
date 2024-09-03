using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.BotSegments.Models.CVService;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    /**
     * <summary>Data for clicking on or moving the mouse to a CV Text location in the scene.
     * This processes the results by finding the smallest bounding rect of the matching texts possible withinRect if specified and clicking on the center of that rect.</summary>
     */
    [Serializable]
    public class CVTextMouseActionData : IBotActionData
    {
        // api version for this object, update if object format changes
        public int apiVersion = SdkApiVersion.VERSION_14;

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

        [Serializable]
        private struct ResultTreeNode
        {
            public List<ResultTreeNode> children;

            public List<int> indexGraph;

            public RectInt? boundsRect;
        }

        private void RequestCVTextEvaluation(int segmentNumber)
        {
            lock (_syncLock)
            {
                if (!_requestInProgress)
                {
                    // Request the CV Text data
                    var screenshot = ScreenshotCapture.GetInstance().GetCurrentScreenshot(segmentNumber, out var width, out var height);
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
                                            var missingWords = new List<string>();
                                            var textParts = text.Trim().Split(' ').Select(a => a.Trim()).Where(a => a.Length > 0).Select(a => textCaseRule == TextCaseRule.Ignore ? a.ToLower() : a).ToList();

                                            var partIndexes = new Dictionary<string, List<int>>();

                                            //  TODO: Find the possible bounding boxes that contain all the words... pick the smallest , or the one that is 'within rect'
                                            // if many matches of the same word then this set gets fairly large, but such is life

                                            // We take a multi pass approach here to make this more understandable for future developers reading it.  originally this was a one pass approach but it was too complex/obscure to be serviceable
                                            // pass1 - get the indexes of each of our textPart words ( remember we may have the same word twice so we'll need to consider that)
                                            // pass2 - using those index buckets, build out matching sets that include all text parts

                                            var cvTextResultsCount = cvTextResults.Count;
                                            var textPartsCount = textParts.Count;

                                            // n^2 looping - but we cover the probably large results list 1 time, and the hopefully short lookup text N times
                                            for (var k = 0; k < cvTextResultsCount; k++)
                                            {
                                                var cvTextResult = cvTextResults[k];
                                                var cvText = cvTextResult.text.Trim();

                                                if (textCaseRule == TextCaseRule.Ignore)
                                                {
                                                    cvText = cvText.ToLower();
                                                }

                                                // see if we can find a matching textPart for this result.. if so.. add this index to that text part tracker
                                                var matched = false;
                                                for (var i = 0; !matched && i <textPartsCount; i++)
                                                {
                                                    var textToMatch = textParts[i];
                                                    switch (textMatchingRule)
                                                    {
                                                        case TextMatchingRule.Matches:
                                                            if (textToMatch.Equals(cvText))
                                                            {
                                                                if (!partIndexes.TryGetValue(textToMatch, out var list))
                                                                {
                                                                    list = new List<int>();
                                                                    partIndexes[textToMatch] = list;
                                                                }
                                                                list.Add(k);
                                                                matched = true;
                                                            }
                                                            break;
                                                        case TextMatchingRule.Contains:
                                                            if (cvText.Contains(textToMatch))
                                                            {
                                                                if (!partIndexes.TryGetValue(textToMatch, out var list))
                                                                {
                                                                    list = new List<int>();
                                                                    partIndexes[textToMatch] = list;
                                                                }
                                                                list.Add(k);
                                                                matched = true;
                                                            }
                                                            break;
                                                    }
                                                }
                                            }

                                            var indexTreeRoot = new ResultTreeNode
                                            {
                                                indexGraph = new(),
                                                children = new(),
                                                boundsRect = null
                                            };

                                            var currentNodes = new List<ResultTreeNode> ();

                                            var nextNodes = new List<ResultTreeNode> ();
                                            nextNodes.Add(indexTreeRoot);

                                            var maxDepth = 0;

                                            // build out the tree of indexes and bounding boxes
                                            for (var i = 0; i < textPartsCount; i++)
                                            {
                                                currentNodes = nextNodes;
                                                nextNodes = new();
                                                var textToMatch = textParts[i];
                                                if (!partIndexes.TryGetValue(textToMatch, out var list))
                                                {
                                                    missingWords.Add(textToMatch);
                                                    break;
                                                }

                                                // process this tree layer computing bounds and tree depth
                                                foreach (var node in currentNodes)
                                                {
                                                    var nodeBounds = node.boundsRect;
                                                    var success = false;

                                                    foreach (var a in list)
                                                    {
                                                        // can't re-use the same index for multiple matches of a word in a tree branch
                                                        if (!node.indexGraph.Contains(a))
                                                        {
                                                            // make sure we had enough of this word
                                                            success = true;

                                                            var boundsRectForThisEntry = cvTextResults[a].rect;
                                                            maxDepth = i + 1;

                                                            // if the tree layer above us had bounds, then find the new bounds rect that encompasses that + our new layer
                                                            RectInt newBoundsRect;
                                                            if (nodeBounds.HasValue)
                                                            {
                                                                var xMin = Math.Min(nodeBounds.Value.xMin, boundsRectForThisEntry.xMin);
                                                                var yMin = Math.Min(nodeBounds.Value.yMin, boundsRectForThisEntry.yMin);
                                                                var xMax = Math.Max(nodeBounds.Value.xMax, boundsRectForThisEntry.xMax);
                                                                var yMax = Math.Max(nodeBounds.Value.yMax, boundsRectForThisEntry.yMax);
                                                                newBoundsRect = new(xMin, yMin, (xMax - xMin), (yMax - yMin));
                                                            }
                                                            else
                                                            {
                                                                newBoundsRect = boundsRectForThisEntry;
                                                            }

                                                            var indexGraph = new List<int>();
                                                            indexGraph.AddRange(node.indexGraph);
                                                            indexGraph.Add(a);
                                                            var newChild = new ResultTreeNode()
                                                            {
                                                                indexGraph = indexGraph,
                                                                children = new(),
                                                                boundsRect = newBoundsRect
                                                            };
                                                            node.children.Add(newChild);

                                                            // add to the tree layer tracker for the next pass
                                                            nextNodes.Add(newChild);
                                                        }
                                                    }

                                                    if (!success)
                                                    {
                                                        missingWords.Add(textToMatch);
                                                    }
                                                }
                                            }

                                            if (maxDepth < textPartsCount)
                                            {
                                                _lastError = $"Missing CVText - text: {WordsToStars(textParts)}, caseRule: {textCaseRule}, matchRule: {textMatchingRule}, missingWords: Tree Depth Too Shallow.. This Shouldn't Happen";
                                            }

                                            if (missingWords.Count != 0)
                                            {
                                                _lastError = $"Missing CVText - text: {WordsToStars(textParts)}, caseRule: {textCaseRule}, matchRule: {textMatchingRule}, missingWords: [{missingWords.Count}] {WordsToStars(missingWords)}";
                                                _cvResultsBoundsRect = null;
                                            }
                                            else
                                            {
                                                _lastError = null;
                                                // find the smallest rect that works
                                                if (withinRect == null)
                                                {
                                                    // just take the smallest rect
                                                    RectInt? smallestRect = null;
                                                    var nextNodesCount = nextNodes.Count;
                                                    for (var i = 0; i < nextNodesCount; i++)
                                                    {
                                                        var nextNode = nextNodes[i];
                                                        if (!smallestRect.HasValue)
                                                        {
                                                            smallestRect = nextNode.boundsRect;
                                                        }
                                                        else
                                                        {
                                                            var br = nextNode.boundsRect.Value;
                                                            if (br.width * br.height < smallestRect.Value.width * smallestRect.Value.height)
                                                            {
                                                                smallestRect = br;
                                                            }
                                                        }
                                                    }
                                                    _cvResultsBoundsRect = smallestRect;
                                                }
                                                else
                                                {
                                                    // find the smallest that intersects our withinRect
                                                    RectInt? smallestRect = null;
                                                    var nextNodesCount = nextNodes.Count;
                                                    for (var i = 0; i < nextNodesCount; i++)
                                                    {
                                                        var nextNode = nextNodes[i];
                                                        if (!smallestRect.HasValue)
                                                        {
                                                            smallestRect = nextNode.boundsRect;
                                                        }
                                                        else
                                                        {
                                                            var br = nextNode.boundsRect.Value;
                                                            if (br.width * br.height < smallestRect.Value.width * smallestRect.Value.height)
                                                            {
                                                                var relativeScaling = new Vector2(withinRect.screenSize.x / (float)cvTextResults[0].resolution.x, withinRect.screenSize.y / (float)cvTextResults[0].resolution.y);

                                                                // check the bottom left and top right to see if it intersects our rect
                                                                var bottomLeft = new Vector2Int(Mathf.CeilToInt(br.xMin * relativeScaling.x), Mathf.CeilToInt(br.yMin * relativeScaling.y));
                                                                var topRight = new Vector2Int(bottomLeft.x + Mathf.FloorToInt(br.width * relativeScaling.x), bottomLeft.y + Mathf.FloorToInt(br.height * relativeScaling.y));

                                                                // we currently test overlap, should we test fully inside instead ??
                                                                if (withinRect.rect.Contains(bottomLeft) || withinRect.rect.Contains(topRight))
                                                                {
                                                                    smallestRect = br;
                                                                }
                                                            }
                                                        }
                                                    }
                                                    _cvResultsBoundsRect = smallestRect;
                                                }
                                            }
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

        public static string WordsToStars(IEnumerable<string> words)
        {
            StringBuilder output = new StringBuilder(100);
            foreach (string word in words)
            {
                foreach (char c in word)
                {
                    output.Append("*");
                }
                output.Append(" ");
            }
            // remove trailing ' '
            return output.ToString().Trim();
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
