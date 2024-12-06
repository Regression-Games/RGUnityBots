using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.BotCriteria;
using RegressionGames.StateRecorder.BotSegments.Models.KeyMoments.BotActions;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder
{
    public class KeyMomentEvaluator
    {

        public readonly List<MouseInputActionData> MouseDataBuffer = new();

        private MouseInputActionData _previousKeyMomentMouseInputState = null;

        private long _keyMomentNumber;

        public void Reset()
        {
            _keyMomentNumber = 0;
        }

        [CanBeNull]
        public BotSegment EvaluateKeyMoment(long tickNumber, out long keyMomentNumber)
        {
            var meaningfulInputs = MouseDataBuffer.Select((data, i) => (i,data)).Where(tuple => tuple.data.clickedObjectNormalizedPaths.Length > 0).ToList();
            if (meaningfulInputs.Count > 0)
            {
                List<MouseInputActionData> inputsToProcess = new();
                // only take from the buffer up to the last 'un-clicked' state
                // this is 'hard'... we want to encapsulate click down/up as a single action together
                // but in cases like FPS where I might 'hold' right click to ADS then repeatedly click/release the left mouse button to fire.. i don't want that ALL as one action
                // each shot of the weapon would be an 'action', AND .. ads was an action.. so we break this up by any button release

                // find the index range we want
                var firstInput = meaningfulInputs[0];

                if (firstInput.data.IsButtonUnClick(_previousKeyMomentMouseInputState))
                {
                    inputsToProcess.Add(firstInput.data);
                    // clean out all the entries up to here
                    MouseDataBuffer.RemoveRange(0, firstInput.i+1);
                }
                else if (meaningfulInputs.Count > 1)
                {
                    var priorData = _previousKeyMomentMouseInputState;
                    var foundUnClick = false;
                    // go through all the inputs until we find an un-click
                    // we speculatively update the list as we go, but if we never hit an un-click we wipe it back out before moving on
                    foreach (var meaningfulInput in meaningfulInputs)
                    {
                        if (meaningfulInput.i - 1 >= 0)
                        {
                            var previousInput = MouseDataBuffer[meaningfulInput.i - 1];

                            // this may seem odd, but we use this API backwards so that we see if the previous mouse spot before the click had a different click state than the current one.. it 'should' ,but better to be safe
                            // ultimately.. we're trying to add the mouse position event before the click so that the mouse is 'in position' before clicking down to avoid any snafu's with the input system
                            if (previousInput.IsButtonUnClick(meaningfulInput.data))
                            {
                                inputsToProcess.Add(previousInput);
                            }
                        }

                        inputsToProcess.Add(meaningfulInput.data);
                        if (meaningfulInput.data.IsButtonUnClick(priorData))
                        {
                            // clean out all the entries up to here
                            MouseDataBuffer.RemoveRange(0, meaningfulInput.i+1);
                            foundUnClick = true;
                            break;
                        }

                        priorData = meaningfulInput.data;
                    }

                    if (!foundUnClick)
                    {
                        // never found an un-click.. don't process the list so far
                        inputsToProcess.Clear();
                    }
                }

                if (inputsToProcess.Count > 0)
                {
                    // update the last state based on the end entry in our list
                    _previousKeyMomentMouseInputState = inputsToProcess[^1];

                    ++_keyMomentNumber;

                    var botSegment = new BotSegment
                    {
                        name = $"KeyMoment: {_keyMomentNumber}, Tick: {tickNumber} - Mouse Action Segment",

                        endCriteria = new List<KeyFrameCriteria>
                        {
                            new()
                            {
                                type = KeyFrameCriteriaType.ActionComplete,
                                data = new ActionCompleteKeyFrameCriteriaData()
                            }
                        },
                        botAction = new BotAction
                        {
                            type = BotActionType.KeyMoment_MouseAction,
                            data = new KeyMomentMouseActionData
                            {
                                mouseActions = inputsToProcess
                            }
                        }
                    };

                    keyMomentNumber = _keyMomentNumber;
                    return botSegment;
                }
            }

            keyMomentNumber = _keyMomentNumber;
            return null;
        }
    }
}
