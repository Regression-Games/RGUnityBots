using System;
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
using UnityEngine.UIElements;

namespace RegressionGames.StateRecorder
{
    public class ReplayDataPlaybackController : MonoBehaviour
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

        private readonly List<ReplayKeyboardInputEntry> _keyboardQueue = new();
        private readonly List<ReplayMouseInputEntry> _mouseQueue = new();

        // helps indicate if we made it through the full replay successfully
        private bool? _replaySuccessful;

        private static IDisposable _mouseEventHandler;

        public string WaitingForKeyFrameConditions { get; private set; }

        public bool KeyFrameInputComplete { get; private set; }

        private void Start()
        {
            GetMouse(true);
            SceneManager.sceneLoaded += OnSceneLoad;
        }

        private void OnDestroy()
        {
            _mouseEventHandler?.Dispose();
            _mouseEventHandler = null;
        }

        void OnSceneLoad(Scene s, LoadSceneMode m)
        {
            // since this is a don't destroy on load, we need to 'fix' the event systems in each new scene that loads
            SetupEventSystem();
        }

        private void SetupEventSystem()
        {
            // when/if we can make the legacy input system work, we should remove this and respect that system
            var eventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            InputSystemUIInputModule inputModule = null;
            foreach (var eventSystem in eventSystems)
            {
                inputModule = eventSystem.gameObject.GetComponent<InputSystemUIInputModule>();
                if (inputModule != null)
                {
                    break;
                }
            }
            if (inputModule == null)
            {
                RGDebug.LogError("Regression Games Unity SDK only supports the new InputSystem, but did not detect an instance of InputSystemUIInputModule in the scene.  If you are using a 3rd party input module like Coherent GameFace this may be expected/ok.");
            }
        }

        void OnEnable()
        {
            SetupEventSystem();
        }

        public void SetDataContainer(ReplayDataContainer dataContainer)
        {
            Stop();

            SendMouseEvent(0, new ReplayMouseInputEntry()
            {
                // get the mouse off the screen, when replay fails, we leave the virtual mouse cursor alone so they can see its location at time of failure, but on new file, we want this gone
                position = new Vector2Int(Screen.width +20, -20)
            }, new List<RecordedGameObjectState>());

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

        private double CurrentTimePoint => Time.unscaledTime - _lastStartTime + _priorKeyFrameTime ?? 0.0;

        private void CheckWaitForKeyStateMatch(List<RecordedGameObjectState> objectStates, string pixelHash)
        {
            if (_isPlaying && _dataContainer != null)
            {
                if (_nextKeyFrame != null)
                {
                    KeyFrameInputComplete = (_keyboardQueue.All(a => a.startEndSentFlags[0]
                                                                     && (a.startEndSentFlags[1] || a.endTime > _nextKeyFrame.time))
                                             && _mouseQueue.Count == 0
                        );
                    WaitingForKeyFrameConditions = CheckKeyFrameState(objectStates, pixelHash);

                    if (WaitingForKeyFrameConditions == null)
                    {
                        RGDebug.LogInfo($"({_nextKeyFrame.tickNumber}) Wait for KeyFrame - ConditionsMatched: true");

                        _priorKeyFrameTime = _nextKeyFrame?.time;

                        // get the next key frame for future stuff
                        _nextKeyFrame = _dataContainer.DequeueKeyFrame();

                        // when we update the keyframe, re-sync our time point to avoid drifting off track
                        _lastStartTime = Time.unscaledTime;

                        if (_nextKeyFrame != null)
                        {
                            // get all the inputs starting before the next key frame time and add them to the queue
                            // floating point hell.. give them 1ms tolerance to avoid fp comparison issues
                            _keyboardQueue.AddRange(_dataContainer.DequeueKeyboardInputsUpToTime(_nextKeyFrame.time + 0.001));
                            _mouseQueue.AddRange(_dataContainer.DequeueMouseInputsUpToTime(_nextKeyFrame.time + 0.001));
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

        private string CheckKeyFrameState(List<RecordedGameObjectState> objectStates, string pixelHash)
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
                        var uiElements = _nextKeyFrame.uiElements.ToList();
                        var specificObjectPaths = _nextKeyFrame.specificObjectPaths.ToList();
                        var gameElements = _nextKeyFrame.gameElements.ToList();
                        foreach (var state in objectStates)
                        {
                            scenes.Remove(state.scene);

                            specificObjectPaths.Remove(state.path);

                            if (state.worldSpaceBounds == null)
                            {
                                if (uiElements.Count == 0)
                                {
                                    return $"({_nextKeyFrame.tickNumber}) Wait for KeyFrame - Unexpected UIElement:\r\n" + state.path;
                                }

                                var didRemove = uiElements.Remove(state.path);
                                if (!didRemove)
                                {
                                    return $"({_nextKeyFrame.tickNumber}) Wait for KeyFrame - Too many instances of UIElement:\r\n" + state.path;
                                }
                            }
                            else
                            {
                                // remove any matching game elements
                                for (var i = gameElements.Count-1; i >= 0; i--)
                                {
                                    var element = gameElements[i];
                                    if (element.Item1 == state.path && element.Item2 == state.rendererCount && element.Item3 == state.colliders.Count && element.Item4 == state.rigidbodies.Count)
                                    {
                                        gameElements.RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                        }

                        if (scenes.Count != 0 || specificObjectPaths.Count != 0 || uiElements.Count != 0 || gameElements.Count != 0)
                        {
                            var missingConditions = $"({_nextKeyFrame.tickNumber}) Wait for KeyFrame - Waiting for conditions...\r\nscenes:\r\n{string.Join("\r\n", scenes)}\r\nuiElements:\r\n{string.Join("\r\n", uiElements)}\r\ngameElements:\r\n{string.Join("\r\n", gameElements.Select(a=> $"({a.Item1}, {a.Item2}, {a.Item3}, {a.Item4})"))}\r\nspecificObjectPaths:\r\n{string.Join("\r\n", specificObjectPaths)}";
                            // still missing something from the key frame
                            return missingConditions;
                        }
                    }
                }

                if (_nextKeyFrame.keyFrameTypes.Contains(KeyFrameType.UI_PIXELHASH) && _nextKeyFrame.pixelHash != null)
                {
                    if (_nextKeyFrame.pixelHash != pixelHash)
                    {
                        return $"({_nextKeyFrame.tickNumber}) Wait for KeyFrame - PixelHash '{pixelHash}' does not match expected '{_nextKeyFrame.pixelHash}'";
                    }
                }
            }

            return null;
        }

        private enum KeyState
        {
            Up,
            Down
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
                        SendKeyEvent(replayKeyboardInputEntry.tickNumber, replayKeyboardInputEntry.key, KeyState.Up);
                        replayKeyboardInputEntry.startEndSentFlags[1] = true;
                    }

                    if (!replayKeyboardInputEntry.startEndSentFlags[0] && currentTime >= replayKeyboardInputEntry.startTime)
                    {
                        // send start event
                        SendKeyEvent(replayKeyboardInputEntry.tickNumber, replayKeyboardInputEntry.key, KeyState.Down);
                        replayKeyboardInputEntry.startEndSentFlags[0] = true;
                    }
                }
            }

            if (_mouseQueue.Count > 0)
            {
                foreach (var replayMouseInputEntry in _mouseQueue)
                {
                    if (currentTime >= replayMouseInputEntry.startTime)
                    {
                        // send event
                        SendMouseEvent(replayMouseInputEntry.tickNumber, replayMouseInputEntry, objectStates);
                        replayMouseInputEntry.IsDone = true;
                    }
                }
            }

            // clean out any inputs that are done
            _keyboardQueue.RemoveAll(a => a.IsDone);
            _mouseQueue.RemoveAll(a => a.IsDone);
        }

        private void SendKeyEvent(long tickNumber, Key key, KeyState upOrDown)
        {
            var keyboard = Keyboard.current;

            if (key == Key.LeftShift || key == Key.RightShift)
            {
                _dataContainer.IsShiftDown = upOrDown == KeyState.Down;
            }

            // 1f == true == pressed state
            // 0f == false == un-pressed state
            using (DeltaStateEvent.From(keyboard, out var eventPtr))
            {
                var time = InputState.currentTime;
                eventPtr.time = time;

                char value = (char)0;
                var inputControl = keyboard.allControls
                    .FirstOrDefault(a => a is KeyControl kc && kc.keyCode == key) ?? keyboard.anyKey;

                if (inputControl != null)
                {
                    RGDebug.LogInfo($"({tickNumber}) Sending Key Event: {key} - {upOrDown}");

                    // queue input event
                    inputControl.WriteValueIntoEvent(upOrDown == KeyState.Down ? 1f : 0f, eventPtr);
                    InputSystem.QueueEvent(eventPtr);

                    if (upOrDown == KeyState.Up)
                    {
                        return;
                    }

                    // send a text event so that 'onChange' text events fire
                    // convert key to text
                    if (KeyboardInputActionObserver.KeyboardKeyToValueMap.TryGetValue(((KeyControl)inputControl).keyCode, out var possibleValues))
                    {
                        value = _dataContainer.IsShiftDown ? possibleValues.Item2 : possibleValues.Item1;
                        if (value == 0x00)
                        {
                            RGDebug.LogError($"Found null value for keyboard input {key}");
                            return;
                        }

                        var inputEvent = TextEvent.Create(Keyboard.current.deviceId, value, time);
                        InputSystem.QueueEvent(ref inputEvent);
                    }
                }
            }
        }

        private static InputDevice GetMouse(bool forceListener = false)
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
                _mouseEventHandler = InputSystem.onEvent.ForDevice(mouse).Call(e =>
                {
                    var positionControl = mouse.allControls.First(a => a.name == "position") as Vector2Control;
                    var position = positionControl.ReadValueFromEvent(e);

                    var buttonsClicked = mouse.allControls.FirstOrDefault(a =>
                        a is ButtonControl abc && abc.ReadValueFromEvent(e) > 0.1f
                    ) != null;
                    RGDebug.LogInfo("Mouse event at: " + position.x + "," + position.y + "  buttonsClicked: " + buttonsClicked);
                    // need to use the static accessor here as this anonymous function's parent gameObject instance could get destroyed
                    FindObjectOfType<VirtualMouseCursor>().SetPosition(position, buttonsClicked);
                });
            }

            if (!mouse.enabled)
            {
                InputSystem.EnableDevice(mouse);
            }

            return mouse;
        }

        public class RecordedGameObjectStatePathEqualityComparer : IEqualityComparer<RecordedGameObjectState>
        {
            public bool Equals(RecordedGameObjectState x, RecordedGameObjectState y)
            {
                return x.path == y.path;
            }

            public int GetHashCode(RecordedGameObjectState obj)
            {
                return obj.path.GetHashCode();
            }
        }

        private static readonly RecordedGameObjectStatePathEqualityComparer _recordedGameObjectStatePathEqualityComparer = new();

        public static void SendMouseEvent(long tickNumber, ReplayMouseInputEntry mouseInput, List<RecordedGameObjectState> objectStates)
        {
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;

            // Mouse is hard... we can't use the raw position, we need to use the position relative to the current resolution
            // but.. it gets tougher than that.  Some UI elements scale differently with resolution (only horizontal, only vertical, preserve aspect, expand, etc)
            // so we have to take the bounding space of the original object(s) clicked on into consideration
            var normalizedPosition = mouseInput.NormalizedPosition;

            var theNp = new Vector3(normalizedPosition.x, normalizedPosition.y, 0f);

            float smallestSize = Screen.width * Screen.height;

            RecordedGameObjectState bestObject = null;

            // find the most precise object clicked on based on paths
            // sorted by relative distance to our normalized position
            var possibleObjects = objectStates
                .Where(a => mouseInput.clickedObjectPaths.Contains(a.path))
                // sort by nearest
                .OrderBy(a =>
                {
                    var closestPointInA = a.screenSpaceBounds.ClosestPoint(theNp);
                    return (theNp - closestPointInA).sqrMagnitude;
                }).Distinct(_recordedGameObjectStatePathEqualityComparer); // select only the first entry of each path
            foreach (var objectToCheck in possibleObjects)
            {
                var size = objectToCheck.screenSpaceBounds.size.x * objectToCheck.screenSpaceBounds.size.y;

                if (bestObject == null)
                {
                    // prefer UI elements when overlaps occur with game objects
                    bestObject = objectToCheck;
                    smallestSize = size;
                }
                else if (objectToCheck.worldSpaceBounds != null)
                {
                    if (bestObject.worldSpaceBounds == null)
                    {
                        //do nothing, prefer ui elements
                    }
                    else
                    {
                        if (mouseInput.worldPosition != null)
                        {
                            // use the world space click location closest to the actual object location
                            var mouseWorldPosition = mouseInput.worldPosition.Value;
                            if (objectToCheck.worldSpaceBounds.Value.Contains(mouseWorldPosition))
                            {
                                var screenPoint = Camera.main.WorldToScreenPoint(mouseWorldPosition);
                                if (screenPoint.x < 0 || screenPoint.x > screenWidth || screenPoint.y < 0 || screenPoint.y > screenHeight)
                                {
                                    RGDebug.LogError($"Attempted to click at worldPosition: [{mouseWorldPosition.x},{mouseWorldPosition.y},{mouseWorldPosition.z}], which is off screen at position: [{screenPoint.x},{screenPoint.y}]");
                                }
                                else
                                {
                                    bestObject = null;
                                    // we hit one of our world objects, set the normalized position and stop looping
                                    normalizedPosition = new Vector2((int)screenPoint.x, (int)screenPoint.y);
                                    break; // end the foreach
                                }
                            }
                            else
                            {
                                // TODO: Maybe??? , should we re-write this to be more like the non world position evaluation and evaluate the 'best' objects to see if we need to shift the world click position a tiny bit to hit our objects
                                // for now, we let that fall back to the renderer bounds evaluation of best objects


                                // compare elements bounds for best match
                                // give some threshold variance here for floating point math on sizes
                                // if 2 objects are very similarly sized, we want the one closest, not picking
                                // one based on some floating point rounding error
                                if (size * 1.02f < smallestSize)
                                {
                                    bestObject = objectToCheck;
                                    smallestSize = size;
                                }
                            }
                        }
                        else
                        {
                            // compare elements bounds for best match
                            // give some threshold variance here for floating point math on sizes
                            // if 2 objects are very similarly sized, we want the one closest, not picking
                            // one based on some floating point rounding error
                            if (size * 1.02f < smallestSize)
                            {
                                bestObject = objectToCheck;
                                smallestSize = size;
                            }
                        }

                    }
                }
                else // objectToCheck.worldSpaceBounds == null
                {
                    if (bestObject.worldSpaceBounds == null)
                    {
                        // compare ui elements for best match
                        // give some threshold variance here for floating point math on sizes
                        // if 2 objects are very similarly sized, we want the one closest, not picking
                        // one based on some floating point rounding error
                        if (size * 1.02f < smallestSize)
                        {
                            bestObject = objectToCheck;
                            smallestSize = size;
                        }
                    }
                    else
                    {
                        // prefer UI elements when overlaps occur with game objects
                        bestObject = objectToCheck;
                        smallestSize = size;
                    }

                }
            }

            if (bestObject != null)
            {
                var clickBounds = bestObject.screenSpaceBounds;

                // evaluate the bounds of the possible objects and narrow our bounding box for any that intersect these bounds
                // ReSharper disable once PossibleMultipleEnumeration
                foreach (var objectToCheck in possibleObjects)
                {
                    if (clickBounds.Intersects(objectToCheck.screenSpaceBounds))
                    {
                        clickBounds.SetMinMax(
                            Vector3.Max(clickBounds.min, objectToCheck.screenSpaceBounds.min),
                            Vector3.Min(clickBounds.max, objectToCheck.screenSpaceBounds.max)
                        );
                    }
                }

                // make sure our click is on that object
                if (!clickBounds.Contains(normalizedPosition))
                {
                    RGDebug.LogInfo($"({tickNumber}) Adjusting click location to ensure hit on object path: " + bestObject.path);
                    // use the center of these bounds as our best point to click
                    normalizedPosition = new Vector2((int)clickBounds.center.x, (int)clickBounds.center.y);
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
                            mouseEventString += $"position: {normalizedPosition.x},{normalizedPosition.y}  ";
                            ((Vector2Control)mouseControl).WriteValueIntoEvent(normalizedPosition, eventPtr);
                            break;
                        case "scroll":
                            if (mouseInput.scroll.x < -0.1f || mouseInput.scroll.x > 0.1f || mouseInput.scroll.y < -0.1f || mouseInput.scroll.y > 0.1f)
                            {
                                mouseEventString += $"scroll: {mouseInput.scroll.x},{mouseInput.scroll.y}  ";
                            }

                            ((DeltaControl)mouseControl).WriteValueIntoEvent(mouseInput.scroll, eventPtr);
                            break;
                        case "leftButton":
                            if (mouseInput.leftButton)
                            {
                                mouseEventString += $"leftButton  ";
                            }

                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.leftButton ? 1f : 0f, eventPtr);
                            break;
                        case "middleButton":
                            if (mouseInput.middleButton)
                            {
                                mouseEventString += $"middleButton  ";
                            }

                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.middleButton ? 1f : 0f, eventPtr);
                            break;
                        case "rightButton":
                            if (mouseInput.rightButton)
                            {
                                mouseEventString += $"rightButton  ";
                            }

                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.rightButton ? 1f : 0f, eventPtr);
                            break;
                        case "forwardButton":
                            if (mouseInput.forwardButton)
                            {
                                mouseEventString += $"forwardButton  ";
                            }

                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.forwardButton ? 1f : 0f, eventPtr);
                            break;
                        case "backButton":
                            if (mouseInput.backButton)
                            {
                                mouseEventString += $"backButton  ";
                            }

                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.backButton ? 1f : 0f, eventPtr);
                            break;
                    }
                }

                RGDebug.LogInfo($"({tickNumber}) Sending Mouse Event - {mouseEventString}");
                InputSystem.QueueEvent(eventPtr);
            }
        }

        public void Update()
        {
            if (_dataContainer != null)
            {
                if (_startPlaying)
                {
                    _lastStartTime = Time.unscaledTime;
                    _startPlaying = false;
                    _isPlaying = true;
                }

                if (_isPlaying)
                {
                    var objectStates = InGameObjectFinder.GetInstance()?.GetStateForCurrentFrame(true);

                    var gameFaceDeltaObserver = GameFacePixelHashObserver.GetInstance();
                    string pixelHash = null;
                    if (gameFaceDeltaObserver != null)
                    {
                        gameFaceDeltaObserver.SetActive(true);
                        // don't clear the value on read during playback
                        pixelHash = gameFaceDeltaObserver.GetPixelHash(false);

                    }
                    CheckWaitForKeyStateMatch(objectStates, pixelHash);

                    PlayInputs(objectStates);
                }
            }

            if (_isPlaying && _nextKeyFrame == null && _keyboardQueue.Count == 0 && _mouseQueue.Count == 0)
            {
                SendMouseEvent(0, new ReplayMouseInputEntry()
                {
                    // get the mouse off the screen, when replay fails, we leave the virtual mouse cursor alone so they can see its location at time of failure
                    position = new Vector2Int(Screen.width +20, -20)
                }, new List<RecordedGameObjectState>());
                // we hit the end of the replay
                Stop();
                _replaySuccessful = true;
            }
        }
    }
}
