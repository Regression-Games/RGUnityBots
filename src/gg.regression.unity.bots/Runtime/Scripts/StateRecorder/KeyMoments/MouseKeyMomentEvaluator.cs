using System.Collections.Generic;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.BotCriteria;
using RegressionGames.StateRecorder.BotSegments.Models.KeyMoments.BotActions;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder.KeyMoments
{
    public class MouseKeyMomentEvaluator : IKeyMomentEvaluator
    {
        public readonly List<MouseInputActionData> MouseDataBuffer = new();

        private MouseInputActionData _previousKeyMomentMouseInputState = null;

        public BotSegment IsKeyMoment(long tickNumber, long keyMomentNumber)
        {
            // build out the list of clicks and un-clicks up to the last un-click
            List<(int, MouseInputActionData)> meaningfulInputs = new();
            MouseInputActionData priorMouseData = _previousKeyMomentMouseInputState;
            var mouseDataBufferCount = MouseDataBuffer.Count;
            var foundUnClick = false;
            for (var i = 0; !foundUnClick && i < mouseDataBufferCount; i++)
            {
                var mouseData = MouseDataBuffer[i];
                var isUnClick = priorMouseData != null && mouseData.IsButtonUnClick(priorMouseData);
                if (mouseData.clickedObjectNormalizedPaths.Length > 0 || isUnClick)
                {
                    meaningfulInputs.Add((i,mouseData));
                }
                if (isUnClick)
                {
                    foundUnClick = true;
                    // we found an un-click, we're done
                }

                priorMouseData = mouseData;
            }

            if (foundUnClick)
            {
                try
                {
                    // this should always be either length 1 - only an un-click, or length 2 - click/un-click pair

                    // only take from the buffer up to the last 'un-clicked' state
                    // this is 'hard'... we want to encapsulate click down/up as a single action together
                    // but in cases like FPS where I might 'hold' right click to ADS then repeatedly click/release the left mouse button to fire.. i don't want that ALL as one action
                    // each shot of the weapon would be an 'action', AND .. ads was an action.. so we break this up by any button release

                    var inputsToProcess = new List<MouseInputActionData>();

                    // add a 'positional' prior state before the click/un-click
                    var priorIndex = meaningfulInputs[0].Item1 - 1;
                    if (priorIndex >= 0)
                    {
                        inputsToProcess.Add(MouseDataBuffer[priorIndex]);
                    }

                    // if meaningfulInputs.Count == 1, then we just had an un-click and that's it... that would only ever happen if you had many buttons held down, and release 1 button 1 frame and another button another frame
                    if (meaningfulInputs.Count > 1)
                    {
                        // add the click
                        inputsToProcess.Add(meaningfulInputs[0].Item2);
                    }

                    // add the un-click
                    inputsToProcess.Add(meaningfulInputs[^1].Item2);

                    var botSegment = new BotSegment
                    {
                        name = $"KeyMoment: {keyMomentNumber}, Tick: {tickNumber} - Mouse Action Segment",

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

                    if (inputsToProcess.Count == 2 && inputsToProcess[^1].clickedObjectNormalizedPaths.Length < 1)
                    {
                        // we had an un-click only segment on something without any paths.. basically an un-click on nothing
                        // this can happen when there is a random click/un-click on nothing in the game or on something that is excluded from RG seeing it in the state

                        // we should NOT record this segment
                        return null;
                    }


                    // update the last state based on the end entry in our list
                    _previousKeyMomentMouseInputState = meaningfulInputs[^1].Item2;

                    return botSegment;
                }
                finally
                {
                    // remove everything up through the un-click
                    MouseDataBuffer.RemoveRange(0, meaningfulInputs[^1].Item1 + 1);
                }
            }

            return null;
        }
    }
}
