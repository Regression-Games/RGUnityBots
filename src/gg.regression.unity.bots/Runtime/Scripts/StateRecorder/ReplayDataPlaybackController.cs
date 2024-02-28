using System.Collections.Generic;
using System.Linq;
using StateRecorder;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

namespace RegressionGames.StateRecorder
{
    public class ReplayDataPlaybackController: MonoBehaviour
    {

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

        private ReplayMouseInputEntry _priorMouseState;

        // helps indicate if we made it through the full replay successfully
        private bool? _replaySuccessful;

        public string WaitingForKeyFrameConditions { get; private set; }

        private void Start()
        {
            GetMouse(true);
            SceneManager.sceneLoaded += OnSceneLoad;
        }

        void OnSceneLoad(Scene s, LoadSceneMode m)
        {
            // since this is a don't destroy on load, we need to 'fix' the event systems in each new scene that loads
            SetupEventSystem();
        }

        private void SetupEventSystem()
        {
            var eventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (var eventSystem in eventSystems)
            {
                var inputModule = eventSystem.gameObject.GetComponent<InputSystemUIInputModule>();
                if (inputModule == null)
                {
                    // forcefully add the UI input module to the event system if not present
                    // basically force using the new input system for our replay
                    eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                }
            }
        }

        void OnEnable()
        {
            SetupEventSystem();
        }

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
            _mouseQueue.Clear();
            _keyboardQueue.Clear();
            _nextKeyFrame = null;
            _isPlaying = false;
            _dataContainer = null;
            WaitingForKeyFrameConditions = null;
            _replaySuccessful = null;
        }

        private double CurrentTimePoint =>  Time.unscaledTime - _lastStartTime + _priorKeyFrameTime??0.0;

        private void CheckWaitForKeyStateMatch(List<RecordedGameObjectState> objectStates)
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
                        WaitingForKeyFrameConditions = CheckKeyFrameState(objectStates);

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

        private string CheckKeyFrameState(List<RecordedGameObjectState> objectStates)
        {
            if (_nextKeyFrame != null)
            {
                if (_nextKeyFrame.specificObjectPaths.Length > 0
                    || _nextKeyFrame.uiElements.Length > 0
                    || _nextKeyFrame.scenes.Length > 0)
                {

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

                                if (state.worldSpaceBounds == null)
                                {
                                    if (uiElements.Count == 0)
                                    {
                                        return "Unexpected UIElement:\r\n" + state.path;
                                    }
                                    var didRemove = uiElements.Remove(state.path);
                                    if (!didRemove)
                                    {
                                        return "Too many instances of UIElement:\r\n" + state.path;
                                    }
                                }
                            }
                        }

                        if (scenes.Count != 0 || specificObjectPaths.Count != 0 || uiElements.Count != 0)
                        {
                            var missingConditions = $"Waiting for conditions...\r\nscenes:\r\n{string.Join("\r\n", scenes)}\r\nuiElements:\r\n{string.Join("\r\n", uiElements)}\r\nspecificObjectPaths:\r\n{string.Join("\r\n", specificObjectPaths)}";
                            if (_firstWaitForKeyFrame)
                            {
                                RGDebug.LogInfo($"KeyFrame: {_nextKeyFrame.tickNumber}" + missingConditions);
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

        private void PlayInputs(List<RecordedGameObjectState> objectStates)
        {
            var currentTime = CurrentTimePoint;
            if (_keyboardQueue.Count > 0)
            {
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
                            SendMouseEvent(replayMouseInputEntry, objectStates);
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
            var keyboard = Keyboard.current;
            // 1f == true == pressed state
            // 0f == false == un-pressed state
            using (DeltaStateEvent.From(keyboard, out var eventPtr))
            {
                eventPtr.time = InputState.currentTime;
                var inputControl = keyboard.allControls
                    .FirstOrDefault(a => a is KeyControl kc && kc.keyCode == key) ?? keyboard.anyKey;

                if (inputControl != null)
                {
                    inputControl.WriteValueIntoEvent(upOrDown == KeyState.Up ? 1f : 0f, eventPtr);
                    RGDebug.LogInfo($"Sending Key Event: {key} - {upOrDown}");
                    InputSystem.QueueEvent(eventPtr);
                }
            }
        }

        private InputDevice GetMouse(bool forceListener = false)
        {
            var mouse = InputSystem.devices.FirstOrDefault(a => a.name == "RGVirtualMouse");
            var created = false;
            if (mouse == null)
            {
                mouse = InputSystem.AddDevice<Mouse>("RGVirtualMouse");
                created = true;
            }

            if (forceListener || created)
            {
                InputSystem.onEvent.ForDevice(mouse).Call(e =>
                {
                    var positionControl = mouse.allControls.First(a => a.name == "position") as Vector2Control;
                    var position = positionControl.ReadValueFromEvent(e);
                    RGDebug.LogInfo("Mouse event at: " +position.x + "," + position.y);
                    // need to use the static accessor here as this anonymous function's parent gameObject instance could get destroyed
                    FindObjectOfType<VirtualMouseCursor>().SetPosition(position);
                });
            }

            if (!mouse.enabled)
            {
                InputSystem.EnableDevice(mouse);
            }

            return mouse;
        }

        private void SendMouseEvent(ReplayMouseInputEntry mouseInput, List<RecordedGameObjectState> objectStates)
        {
            // Mouse is hard... we can't use the raw position, we need to use the position relative to the current resolution
            // but.. it gets tougher than that.  Some UI elements scale differently with resolution (only horizontal, only vertical, preserve aspect, expand, etc)
            // so we have to take the bounding space of the original object(s) clicked on into consideration
            var normalizedPosition = mouseInput.NormalizedPosition;

            float smallestSize = Screen.width * Screen.height;

            RecordedGameObjectState bestObject = null;

            // find the most precise object clicked on
            foreach (var objectToCheck in objectStates.Where(a => mouseInput.clickedObjectPaths.Contains(a.path)))
            {
                var size = objectToCheck.screenSpaceBounds.size.x * objectToCheck.screenSpaceBounds.size.y;
                if (size < smallestSize)
                {
                    bestObject = objectToCheck;
                    smallestSize = size;
                }
            }

            if (bestObject != null)
            {
                // make sure our click is on that object
                if (!bestObject.screenSpaceBounds.Contains(normalizedPosition)
                      && !bestObject.screenSpaceBounds.Contains(new Vector3(mouseInput.position.x, mouseInput.position.y)))
                {
                    // use the center of these bounds as our best point to click
                    normalizedPosition = bestObject.screenSpaceBounds.center;
                }
            }

            var mouse = GetMouse();

            using (DeltaStateEvent.From(mouse, out var eventPtr))
            {
                eventPtr.time = InputState.currentTime;

                var mouseControls = mouse.allControls;
                var mouseEventString = "";

                // 1f == true == clicked state
                // 0f == false == un-clicked state
                foreach (var mouseControl in mouseControls)
                {
                    var controlName = mouseControl.path.Substring(mouseControl.path.LastIndexOf('/') + 1);
                    switch (controlName)
                    {
                        case "position":
                            mouseEventString += $"position: {normalizedPosition.x},{normalizedPosition.y} ";
                            ((Vector2Control)mouseControl).WriteValueIntoEvent(normalizedPosition, eventPtr);
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
                    var objectStates = InGameObjectFinder.GetInstance()?.GetStateForCurrentFrame();
                    CheckWaitForKeyStateMatch(objectStates);

                    PlayInputs(objectStates);
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
