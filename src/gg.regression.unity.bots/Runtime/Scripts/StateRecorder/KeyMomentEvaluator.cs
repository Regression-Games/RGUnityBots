
using System.Collections.Generic;
using JetBrains.Annotations;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.KeyMoments;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder
{
    public class KeyMomentEvaluator
    {

        private long _keyMomentNumber;

        public void Reset()
        {
            _previousMouseEvent = null;
            _keyMomentNumber = 1;
        }

        private readonly MouseKeyMomentEvaluator _mouseKeyMomentEvaluator = new();
        private readonly CVImageMouseKeyMomentEvaluator _cvImageMouseKeyMomentEvaluator = new();

        [CanBeNull]
        public BotSegment EvaluateKeyMoment(long tickNumber, out long keyMomentNumber)
        {
            // Future Consideration ???: This currently considers that only 1 key moment action will be returned per segment.  Maybe this is fine, but we may need to handle returning multiple segments for a single pass at some point,
            // or maybe better would be smashing these into a BotSegmentList.  Since it would be ambiguous to try to replay N actions at the same time, these will have to be sequenced in some way, OR we'll need to create
            // evaluator/action-class pairs that blend things as we need.  (ie: Mouse/Keyboard interleaving, Mouse/GameAction interleaving, etc)  TBD if this is a real thing or just a possibility being considered.
            keyMomentNumber = _keyMomentNumber;

            var result = _mouseKeyMomentEvaluator.IsKeyMoment(tickNumber, _keyMomentNumber);
            result ??= _cvImageMouseKeyMomentEvaluator.IsKeyMoment(tickNumber, _keyMomentNumber);

            if (result != null)
            {
                keyMomentNumber = _keyMomentNumber;
                // increment after reporting in the result
                ++_keyMomentNumber;
            }

            return result;
        }

        private MouseInputActionData _previousMouseEvent = null;

        public void UpdateMouseInputData(long tickNumber, List<MouseInputActionData> mouseInputDataList)
        {
            var mouseInputDataListCount = mouseInputDataList.Count;
            if (mouseInputDataListCount > 0)
            {
                // figure out how many click/un-click events are in this buffer update and add that many copies of the screenshot to the queue
                // ideally each update would have no more than 1 click or un-click but ya never know with how input happens
                var ssCount = 0;
                var priorMouseInputData = _previousMouseEvent;
                for (var i = 0; i < mouseInputDataListCount; i++)
                {
                    var mouseInputData = mouseInputDataList[i];
                    if (mouseInputData.IsButtonClick(priorMouseInputData))
                    {
                        // add the previousEvent + our click
                        if (priorMouseInputData != null)
                        {
                            _mouseKeyMomentEvaluator.MouseDataBuffer.Add(priorMouseInputData);
                            _cvImageMouseKeyMomentEvaluator.MouseDataBuffer.Add(priorMouseInputData);
                        }

                        _mouseKeyMomentEvaluator.MouseDataBuffer.Add(mouseInputData);
                        _cvImageMouseKeyMomentEvaluator.MouseDataBuffer.Add(mouseInputData);
                        ++ssCount;
                    }
                    else if (mouseInputData.IsButtonUnClick(priorMouseInputData))
                    {
                        _mouseKeyMomentEvaluator.MouseDataBuffer.Add(mouseInputData);
                        _cvImageMouseKeyMomentEvaluator.MouseDataBuffer.Add(mouseInputData);
                        ++ssCount;
                    }

                    priorMouseInputData = mouseInputData;
                }

                if (ssCount > 0)
                {
                    ScreenshotCapture.GetInstance().GetCurrentScreenshotWithCallback(
                        tickNumber,
                        (result) =>
                        {
                            if (result.HasValue)
                            {
                                for (int j = 0; j < ssCount; j++)
                                {
                                    _cvImageMouseKeyMomentEvaluator.ImageBuffer.Add(result.Value);
                                }
                            }
                        });
                }

                _previousMouseEvent = mouseInputDataList[^1];
            }
        }
    }
}
