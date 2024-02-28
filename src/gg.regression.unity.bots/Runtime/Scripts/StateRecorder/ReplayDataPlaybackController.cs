using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace RegressionGames.StateRecorder
{
    public class ReplayDataPlaybackController: MonoBehaviour
    {

        [Tooltip("Set true to replay the recording as fast as possible while respecting key frames.  Set false to watch the replay closer to the timing of the original recording.")]
        public bool rapidReplay;

        private ReplayDataContainer _dataContainer;

        // flag to indicate the next update loop should start playing
        private bool _startPlaying;

        //tracks in playback is in progress or paused
        private bool _isPlaying;

        // tracks the when we started or last got a key frame; tracked in unscaled time
        private float _lastStartTime;

        private double? _priorKeyFrameTime;

        private ReplayKeyFrameEntry _nextKeyFrame;

        private readonly List<ReplayKeyboardInputEntry> _keyboardQueue = new ();
        private readonly List<ReplayMouseInputEntry> _mouseQueue = new();

        private ReplayMouseInputEntry _priorMouseState = null;

        // helps indicate if we made it through the full replay successfully
        private bool? _replaySuccessful;

        public string WaitingForKeyFrameConditions { get; private set; }

        public void SetDataContainer(ReplayDataContainer dataContainer)
        {
            _isPlaying = false;
            _dataContainer = dataContainer;
            _nextKeyFrame = _dataContainer.DequeueKeyFrame();
        }

        /**
         * <summary>Returns true only when the replay is complete and successful</summary>
         */
        public bool? ReplayCompletedSuccessfully()
        {
            return _replaySuccessful;
        }

        public void Play()
        {
            if (_dataContainer != null)
            {
                if (!_startPlaying && !_isPlaying)
                {
                    _startPlaying = true;
                }
            }
        }

        public void Stop()
        {
            _isPlaying = false;
            _dataContainer = null;
            WaitingForKeyFrameConditions = null;
            _replaySuccessful = null;
        }

        private double CurrentTimePoint =>  Time.unscaledTime - _lastStartTime + _priorKeyFrameTime??0.0;

        private void CheckWaitForKeyStateMatch()
        {
            if (_isPlaying && _dataContainer != null )
            {
                if (_nextKeyFrame != null)
                {
                    // check that we've started all the last inputs as we got those up to the next key frame time
                    // if we haven't started all those yet, we shouldn't be checking the next key frame time
                    if (_keyboardQueue.All(a => a.startEndSentFlags[0]
                            && (a.startEndSentFlags[1] || a.endTime > _nextKeyFrame.time))
                        && _mouseQueue.Count == 0
                        )
                    {
                        // we've started all the inputs but have we hit the key frame state yet?
                        WaitingForKeyFrameConditions = CheckKeyFrameState();

                        if (WaitingForKeyFrameConditions == null)
                        {
                            RGDebug.LogInfo($"Wait for KeyFrame: {_nextKeyFrame.tickNumber} - ConditionsMatched: true");

                            _priorKeyFrameTime = _nextKeyFrame?.time;
                            _firstWaitForKeyFrame = true;

                            // get the next key frame for future stuff
                            _nextKeyFrame = _dataContainer.DequeueKeyFrame();

                            // when we update the keyframe, re-sync our time point to avoid drifting off track
                            _lastStartTime =  Time.unscaledTime;

                            if (_nextKeyFrame != null)
                            {
                                // get all the inputs starting before the next key frame time and add them to the queue
                                _keyboardQueue.AddRange(_dataContainer.DequeueKeyboardInputsUpToTime(_nextKeyFrame.time));
                                _mouseQueue.AddRange(_dataContainer.DequeueMouseInputsUpToTime(_nextKeyFrame.time));
                            }
                            else
                            {
                                WaitingForKeyFrameConditions = null;
                                // get the rest
                                _keyboardQueue.AddRange(_dataContainer.DequeueKeyboardInputsUpToTime());
                                _mouseQueue.AddRange(_dataContainer.DequeueMouseInputsUpToTime());
                            }
                        }
                    }
                }
            }
        }

        private bool _firstWaitForKeyFrame = true;

        private string CheckKeyFrameState()
        {
            if (_nextKeyFrame != null)
            {
                var currentTime = CurrentTimePoint;
                if (!rapidReplay && currentTime < _nextKeyFrame.time)
                {
                    return "Time until next key frame: " + (_nextKeyFrame.time-currentTime);
                    // waiting for timing of next key frame
                }

                if (_nextKeyFrame.specificObjectPaths.Length > 0
                    || _nextKeyFrame.uiElements.Length > 0
                    || _nextKeyFrame.scenes.Length > 0)
                {
                    var objectStates = InGameObjectFinder.GetInstance()?.GetStateForCurrentFrame();
                    if (objectStates != null)
                    {
                        // copy these so we can remove from them
                        var scenes = _nextKeyFrame.scenes.ToList();
                        // don't validate Regression Games overlay stuff
                        var uiElements = _nextKeyFrame.uiElements.Where(a => !a.StartsWith("RGOverlay")).ToList();
                        var specificObjectPaths = _nextKeyFrame.specificObjectPaths.Where(a => !a.StartsWith("RGOverlay")).ToList();
                        foreach (var state in objectStates)
                        {
                            scenes.Remove(state.scene);

                            //don't validate Regression Games overlay stuff
                            if (!state.path.StartsWith("RGOverlay"))
                            {
                                specificObjectPaths.Remove(state.path);

                                if (_nextKeyFrame.uiElements.Contains(state.path) && uiElements.Count == 0)
                                {
                                    // we do strict enforcement that the UI elements match EXACTLY
                                    // if we've already removed all instances of this one, that's a failure to match key frame
                                    return "Too many instances of UIElement: " + state.path;

                                }

                                uiElements.Remove(state.path);
                            }
                        }

                        if (scenes.Count != 0 || specificObjectPaths.Count != 0 || uiElements.Count != 0)
                        {
                            var missingConditions = $"scenes:\r\n  {string.Join("\r\n  ", scenes)}\r\nuiElements:\r\n  {string.Join("\r\n  ", uiElements)}\r\nspecificObjectPaths:\r\n  {string.Join("\r\n  ", specificObjectPaths)}";
                            if (_firstWaitForKeyFrame)
                            {
                                RGDebug.LogInfo($"Waiting for KeyFrame: {_nextKeyFrame.tickNumber} - conditions...\r\n" + missingConditions);
                            }
                            _firstWaitForKeyFrame = false;
                            // still missing something from the key frame
                            return missingConditions;
                        }
                    }
                }
            }
            return null;
        }

        private enum KeyState
        {
            Up, Down
        }

        private void PlayInputs()
        {
            var currentTime = CurrentTimePoint;
            if (_keyboardQueue.Count > 0)
            {
                var keyboardDevice = InputSystem.GetDevice<Keyboard>();
                foreach (var replayKeyboardInputEntry in _keyboardQueue)
                {
                    if (!replayKeyboardInputEntry.startEndSentFlags[1] && currentTime >= replayKeyboardInputEntry.endTime)
                    {
                        // send end event
                        SendKeyEvent(replayKeyboardInputEntry.key, KeyState.Up);
                        replayKeyboardInputEntry.startEndSentFlags[1] = true;
                    }

                    if (!replayKeyboardInputEntry.startEndSentFlags[0] && currentTime >= replayKeyboardInputEntry.startTime)
                    {
                        // send start event
                        SendKeyEvent(replayKeyboardInputEntry.key, KeyState.Down);
                        replayKeyboardInputEntry.startEndSentFlags[0] = true;
                    }

                }
            }

            if (_mouseQueue.Count > 0)
            {
                foreach (var replayMouseInputEntry in _mouseQueue)
                {
                    // compare to the prior mouse state event to send the right event

                    // end before start so get at least 1 frame between start/end events
                    if (currentTime >= replayMouseInputEntry.startTime)
                    {
                        // send event
                        if (_priorMouseState != null)
                        {
                            SendMouseEvent(replayMouseInputEntry);
                        }
                        replayMouseInputEntry.IsDone = true;
                        _priorMouseState = replayMouseInputEntry;
                    }
                }
            }

            // clean out any inputs that are done
            _keyboardQueue.RemoveAll(a => a.IsDone);
            _mouseQueue.RemoveAll(a => a.IsDone);
        }

        private void SendKeyEvent(Key key, KeyState upOrDown)
        {
            var keyboardDevice = Keyboard.current;
            // 1f == true == pressed state
            // 0f == false == un-pressed state
            void SetUpAndQueueEvent<TValue>(InputEventPtr eventPtr, TValue state) where TValue : struct
            {
                eventPtr.time = InputState.currentTime;
                var inputControl = keyboardDevice.allControls
                    .FirstOrDefault(a => a is KeyControl kc && kc.keyCode == key);
                if (inputControl == null)
                {
                    inputControl = keyboardDevice.allControls.FirstOrDefault(a => a is AnyKeyControl);
                }

                if (inputControl != null)
                {
                    inputControl.WriteValueIntoEvent(state, eventPtr);
                    InputSystem.QueueEvent(eventPtr);
                }
            }

            using (DeltaStateEvent.From(keyboardDevice, out var eventPtr))
            {
                RGDebug.LogInfo($"Sending Key Event: {key} - {upOrDown}");
                SetUpAndQueueEvent(eventPtr, upOrDown == KeyState.Up ? 1f : 0f);
            }
        }

        private void SendMouseEvent(ReplayMouseInputEntry mouseInput)
        {
            var mouseDevice = Mouse.current;
            using (StateEvent.From(mouseDevice, out var eventPtr))
            {
                eventPtr.time = InputState.currentTime;

                var mouseControls = mouseDevice.allControls;
                var mouseEventString = "";
                foreach (var mouseControl in mouseControls)
                {
                    var controlName = mouseControl.path.Substring(mouseControl.path.LastIndexOf('/') + 1);
                    switch (controlName)
                    {
                        case "position":
                            mouseEventString += $"position: {mouseInput.position.x},{mouseInput.position.y} ";
                            ((Vector2Control)mouseControl).WriteValueIntoEvent(mouseInput.position, eventPtr);
                            break;
                        case "scroll":
                            if (mouseInput.scroll.x < -0.1f || mouseInput.scroll.x > 0.1f || mouseInput.scroll.y < -0.1f || mouseInput.scroll.y > 0.1f)
                            {
                                mouseEventString += $"scroll: {mouseInput.scroll.x},{mouseInput.scroll.y} ";
                            }
                            ((DeltaControl)mouseControl).WriteValueIntoEvent(mouseInput.scroll, eventPtr);
                            break;
                        case "leftButton":
                            if (mouseInput.leftButton)
                            {
                                mouseEventString += $"leftButton ";
                            }
                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.leftButton?1f:0f, eventPtr);
                            break;
                        case "middleButton":
                            if (mouseInput.middleButton)
                            {
                                mouseEventString += $"middleButton ";
                            }
                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.middleButton?1f:0f, eventPtr);
                            break;
                        case "rightButton":
                            if (mouseInput.rightButton)
                            {
                                mouseEventString += $"rightButton ";
                            }
                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.rightButton?1f:0f, eventPtr);
                            break;
                        case "forwardButton":
                            if (mouseInput.forwardButton)
                            {
                                mouseEventString += $"forwardButton ";
                            }
                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.forwardButton?1f:0f, eventPtr);
                            break;
                        case "backButton":
                            if (mouseInput.backButton)
                            {
                                mouseEventString += $"backButton ";
                            }
                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.backButton?1f:0f, eventPtr);
                            break;
                    }

                    mouseEventString += ", ";
                }
                RGDebug.LogInfo($"Sending Mouse Event - {mouseEventString}");
                InputSystem.QueueEvent(eventPtr);
            }
        }

        public void Update()
        {
            if (_dataContainer != null)
            {
                if (_startPlaying)
                {
                    _lastStartTime =  Time.unscaledTime;
                    _startPlaying = false;
                    _isPlaying = true;
                }

                if (_isPlaying)
                {
                    CheckWaitForKeyStateMatch();

                    PlayInputs();
                }
            }
            if (_isPlaying && _nextKeyFrame == null && _keyboardQueue.Count == 0 && _mouseQueue.Count == 0)
            {
                // we hit the end of the replay
                Stop();
                _replaySuccessful = true;
            }
        }
    }
}
