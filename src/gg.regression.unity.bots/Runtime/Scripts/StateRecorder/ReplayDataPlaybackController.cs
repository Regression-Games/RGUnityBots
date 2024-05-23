using System;
using System.Collections.Generic;
using System.Linq;
using StateRecorder;
#if ENABLE_LEGACY_INPUT_MANAGER
using RegressionGames.RGLegacyInputUtility;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
// ReSharper disable MergeIntoPattern

namespace RegressionGames.StateRecorder
{
    public class ReplayDataPlaybackController : MonoBehaviour
    {
        [Tooltip("UI Element keyframe enforcement mode during replay.  Default is 'Delta'")]
        public UIReplayEnforcement uiReplayEnforcement = UIReplayEnforcement.Delta;

        private ReplayDataContainer _dataContainer;

        // flag to indicate the next update loop should start playing
        private bool _startPlaying;

        //tracks in playback is in progress or paused
        private bool _isPlaying;

        // 0 or greater == isLooping true
        private int _loopCount = -1;

        private Action<int> _loopCountCallback;

        // tracks the when we started or last got a key frame; tracked in unscaled time
        private float _lastStartTime;

        private double? _priorKeyFrameTime;

        // We track this as a list instead of a single entry to allow the UI and game object conditions to evaluate separately
        // We still only unlock the input sequences for a key frame once both UI and game object conditions are met
        // This is done this way to allow situations like when loading screens (UI) are changing while game objects are loading in the background and the process is not consistent/deterministic between the 2
        private readonly List<ReplayKeyFrameEntry> _nextKeyFrames = new();

        private readonly List<ReplayKeyboardInputEntry> _keyboardQueue = new();
        private readonly List<ReplayMouseInputEntry> _mouseQueue = new();

        // helps indicate if we made it through the full replay successfully
        private bool? _replaySuccessful;

        private static IDisposable _mouseEventHandler;

        public string WaitingForKeyFrameConditions { get; private set; }

        public bool KeyFrameInputComplete { get; private set; }

        private ScreenRecorder _screenRecorder;

        private void Start()
        {
            SetupEventSystem();
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
                var theModule = eventSystem.gameObject.GetComponent<InputSystemUIInputModule>();
                if (theModule != null)
                {
                    inputModule = theModule;
                }
                else
                {
                    // force add it to at least make mouse clicks work on UI elements
                    eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                }
            }
            if (inputModule == null)
            {
                RGDebug.LogError("Regression Games Unity SDK only supports the new InputSystem, but did not detect an instance of InputSystemUIInputModule in the scene.  If you are using a 3rd party input module like Coherent GameFace this may be expected/ok.");
            }
        }

        void OnEnable()
        {
            _screenRecorder = GetComponentInParent<ScreenRecorder>();
            SetupEventSystem();
            GetMouse(true);
        }

        private void OnDisable()
        {
            _mouseEventHandler?.Dispose();
            _mouseEventHandler = null;
        }

        public void SetDataContainer(ReplayDataContainer dataContainer)
        {
            Stop();
            _replaySuccessful = null;

            SendMouseEvent(0, new ReplayMouseInputEntry()
            {
                // get the mouse off the screen, when replay fails, we leave the virtual mouse cursor alone so they can see its location at time of failure, but on new file, we want this gone
                position = new Vector2Int(Screen.width +20, -20)
            }, null, ScreenRecorder._emptyStateDictionary);

            _dataContainer = dataContainer;
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
                    _replaySuccessful = null;
                    _startPlaying = true;
                    _loopCount = -1;
                }
            }
        }

        public void Loop(Action<int> loopCountCallback)
        {
            if (_dataContainer != null)
            {
                if (!_startPlaying && !_isPlaying)
                {
                    _replaySuccessful = null;
                    _startPlaying = true;
                    _loopCount = 1;
                    _loopCountCallback = loopCountCallback;
                    _loopCountCallback.Invoke(_loopCount);
                }
            }
        }

        public void Stop()
        {
            _mouseQueue.Clear();
            _keyboardQueue.Clear();
            _nextKeyFrames.Clear();
            _startPlaying = false;
            _isPlaying = false;
            _loopCount = -1;
            _replaySuccessful = null;
            WaitingForKeyFrameConditions = null;
            _checkOfKeyFrameCount = 0;

            _screenRecorder.StopRecording();
            #if ENABLE_LEGACY_INPUT_MANAGER
            RGLegacyInputWrapper.StopSimulation();
            #endif

            _dataContainer = null;
            InGameObjectFinder.GetInstance()?.Cleanup();
        }

        public void Reset()
        {
            _mouseQueue.Clear();
            _keyboardQueue.Clear();
            _nextKeyFrames.Clear();
            _startPlaying = false;
            _isPlaying = false;
            _loopCount = -1;
            _replaySuccessful = null;
            WaitingForKeyFrameConditions = null;
            _checkOfKeyFrameCount = 0;

            _screenRecorder.StopRecording();

            // similar to Stop, but assumes will play again
            _dataContainer?.Reset();

            InGameObjectFinder.GetInstance()?.Cleanup();
        }

        public void ResetForLooping()
        {
            _mouseQueue.Clear();
            _keyboardQueue.Clear();
            _nextKeyFrames.Clear();
            _startPlaying = true;
            _isPlaying = false;
            // don't change _loopCount
            _replaySuccessful = null;
            WaitingForKeyFrameConditions = null;
            _checkOfKeyFrameCount = 0;

            // similar to Stop, but assumes continued looping .. doesn't stop recording
            _dataContainer?.Reset();

            InGameObjectFinder.GetInstance()?.Cleanup();
        }

        public bool IsPlaying()
        {
            return _isPlaying;
        }

        private double CurrentTimePoint => Time.unscaledTime - _lastStartTime + _priorKeyFrameTime ?? 0.0;

        // used to avoid spam building/showing the keyframe text until at least a few frames have gone past
        private int _checkOfKeyFrameCount;

        // allow up to 360 frames before saying it failed, at 120 fps this is 3 seconds, at 100fps this is ~3.6 seconds , at 60fps this is 6 seconds , at 50 fps this is ~7.2 seconds, at 30 fps this is 12 seconds
        private const int KeyFrameChecksBeforePrompting = 360;

        private void CheckWaitForKeyStateMatch(Dictionary<int,RecordedGameObjectState> objectStates, string pixelHash)
        {
            if (_isPlaying && _dataContainer != null)
            {
                if (_nextKeyFrames.Count > 0)
                {
                    KeyFrameInputComplete = (_keyboardQueue.All(a => a.startEndSentFlags[0]
                                                                     && (a.startEndSentFlags[1] || a.endTime > _nextKeyFrames[0].time))
                                             && _mouseQueue.Count == 0
                        );
                    WaitingForKeyFrameConditions = CheckKeyFrameState(_checkOfKeyFrameCount, objectStates, pixelHash);
                    ++_checkOfKeyFrameCount;

                    var lastKeyFrameInList = _nextKeyFrames[^1];
                    // if either the ui or the game objects are satisfied, add the next key frame to the list so we can start considering it
                    if (lastKeyFrameInList.uiMatched || lastKeyFrameInList.gameMatched)
                    {
                        RGDebug.LogInfo($"{lastKeyFrameInList.tickNumber} Wait for KeyFrame - uiMatched: {lastKeyFrameInList.uiMatched} , gameMatched: {lastKeyFrameInList.gameMatched}");
                        var nextFrame = _dataContainer.DequeueKeyFrame();
                        if (nextFrame != null)
                        {
                            // get the next key frame for future stuff
                            _nextKeyFrames.Add(nextFrame);
                            _checkOfKeyFrameCount = 0;
                        }
                    }

                    while (_nextKeyFrames.Count > 0 && _nextKeyFrames[0].IsMatched)
                    {
                        RGDebug.LogInfo($"{_nextKeyFrames[0].tickNumber} Wait for KeyFrame - ConditionsMatched: true");

                        _priorKeyFrameTime = _nextKeyFrames[0]?.time;

                        // first key frame is done, get it out of here
                        _nextKeyFrames.RemoveAt(0);

                        // when we update the keyframe, re-sync our time point to avoid drifting off track
                        _lastStartTime = Time.unscaledTime;

                        // do this every time we pop one so we don't miss any frames of input
                        if (_nextKeyFrames.Count > 0)
                        {
                            // get all the inputs starting before the next key frame time and add them to the queue
                            // floating point hell.. give them 1ms tolerance to avoid fp comparison issues
                            _keyboardQueue.AddRange(_dataContainer.DequeueKeyboardInputsUpToTime(_nextKeyFrames[0].time + 0.001));
                            _mouseQueue.AddRange(_dataContainer.DequeueMouseInputsUpToTime(_nextKeyFrames[0].time + 0.001));
                        }
                    }

                    // handles the first pass case
                    if (_nextKeyFrames.Count > 0)
                    {
                        // get all the inputs starting before the next key frame time and add them to the queue
                        // floating point hell.. give them 1ms tolerance to avoid fp comparison issues
                        _keyboardQueue.AddRange(_dataContainer.DequeueKeyboardInputsUpToTime(_nextKeyFrames[0].time + 0.001));
                        _mouseQueue.AddRange(_dataContainer.DequeueMouseInputsUpToTime(_nextKeyFrames[0].time + 0.001));
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

        private string CheckKeyFrameState(int keyFrameCheckCount, Dictionary<int,RecordedGameObjectState> objectStates, string pixelHash)
        {
            var nextKeyFramesCount = _nextKeyFrames.Count;
            if (nextKeyFramesCount > 0 && objectStates != null)
            {
                ReplayKeyFrameEntry gameObjectKeyFrame = null, uiObjectKeyFrame = null;
                for (var index = 0; index < nextKeyFramesCount; index++)
                {
                    var nextKeyFrame = _nextKeyFrames[index];
                    if (gameObjectKeyFrame == null && nextKeyFrame.gameMatched == false)
                    {
                        gameObjectKeyFrame = nextKeyFrame;
                    }

                    if (uiObjectKeyFrame == null && nextKeyFrame.uiMatched == false)
                    {
                        uiObjectKeyFrame = nextKeyFrame;
                    }

                    if (gameObjectKeyFrame != null && uiObjectKeyFrame != null)
                    {
                        break;
                    }
                }

                var eldestKeyFrame = uiObjectKeyFrame == null ? gameObjectKeyFrame : gameObjectKeyFrame == null ? uiObjectKeyFrame : uiObjectKeyFrame.tickNumber < gameObjectKeyFrame.tickNumber ? uiObjectKeyFrame : gameObjectKeyFrame;

                // copy these with .ToList() / .ToDictionary() so we can remove from them
                var gameScenes = gameObjectKeyFrame?.gameScenes.ToList();
                var uiScenes = uiReplayEnforcement == UIReplayEnforcement.None ? null : uiObjectKeyFrame?.uiScenes.ToList();

                var uiElements = uiReplayEnforcement == UIReplayEnforcement.None ? null : uiObjectKeyFrame?.uiElementsCounts.ToDictionary(a=>a.Key, a=>a.Value);
                var gameElements = gameObjectKeyFrame?.gameElements.ToList();

                // if no UI elements at all, enforce strict as this is likely a scene transition
                var myReplayEnforcementMode = uiElements?.Count == 0 ? UIReplayEnforcement.Strict : uiReplayEnforcement;

                foreach (var state in objectStates.Values)
                {
                    var hashCode = state.path.GetHashCode();
                    var collidersCount = state.colliders.Count;
                    var rigidbodiesCount = state.rigidbodies.Count;
                    if (state.worldSpaceBounds == null)
                    {
                        if (uiObjectKeyFrame != null)
                        {
                            if (uiScenes != null && uiScenes.Count > 0 )
                            {
                                StateRecorderUtils.OptimizedRemoveStringFromList(uiScenes, state.scene.name);
                            }
                            else
                            {
                                uiScenes = null;
                            }

                            if (uiElements != null)
                            {
                                if (uiElements.TryGetValue(hashCode, out var count))
                                {
                                    if (myReplayEnforcementMode == UIReplayEnforcement.Strict && count.Item2 <= 0)
                                    {
                                        // only bail out here if we are where the ui is the oldest awaited key frame
                                        if (eldestKeyFrame.tickNumber == uiObjectKeyFrame.tickNumber)
                                        {
                                            if (keyFrameCheckCount < KeyFrameChecksBeforePrompting)
                                            {
                                                return null;
                                            }

                                            return uiObjectKeyFrame.tickNumber + " Wait for KeyFrame - (Strict) Too many instances of UIElement:\r\n" + state.path;
                                        }
                                    }
                                    uiElements[hashCode] = (count.Item1, count.Item2 - 1);
                                }
                                else
                                {
                                    if (myReplayEnforcementMode == UIReplayEnforcement.Strict)
                                    {
                                        // only bail out here if we are where the ui is the oldest awaited key frame
                                        if (eldestKeyFrame.tickNumber == uiObjectKeyFrame.tickNumber)
                                        {
                                            if (keyFrameCheckCount < KeyFrameChecksBeforePrompting)
                                            {
                                                return null;
                                            }

                                            return uiObjectKeyFrame.tickNumber + " Wait for KeyFrame - (Strict) Unexpected UIElement:\r\n" + state.path;
                                        }
                                    }
                                    else
                                    {
                                        // start to track the count of un-expected stuff
                                        uiElements[hashCode] = (state.path, -1);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (gameObjectKeyFrame != null)
                        {
                            if (gameScenes != null && gameScenes.Count > 0 )
                            {
                                StateRecorderUtils.OptimizedRemoveStringFromList(gameScenes, state.scene.name);
                            }
                            else
                            {
                                gameScenes = null;
                            }

                            // remove any matching game elements
                            for (var i = gameElements.Count - 1; i >= 0; i--)
                            {
                                var element = gameElements[i];
                                if (element.Item2 == state.rendererCount && element.Item3 == collidersCount && element.Item4 == rigidbodiesCount && string.CompareOrdinal(element.Item1, state.path) == 0)
                                {
                                    gameElements.RemoveAt(i);
                                    break;
                                }
                            }
                        }
                    }
                }

                string gameElementsErrorMessage = null;
                if (gameObjectKeyFrame != null)
                {
                    if (gameElements.Count != 0)
                    {
                        gameElementsErrorMessage = gameObjectKeyFrame.tickNumber + " Wait for KeyFrame - Missing GameElements:\r\n" + string.Join("\r\n", gameElements.Select(a => a.Item1)) + "\r\n";
                    }

                    if (gameScenes != null && gameScenes.Count != 0)
                    {
                        gameElementsErrorMessage = (gameElementsErrorMessage ?? "") + gameObjectKeyFrame.tickNumber + " Wait for KeyFrame - Missing GameScenes:\r\n" + string.Join("\r\n", gameScenes) + "\r\n";
                    }

                    if ((gameScenes == null || gameScenes.Count == 0) && gameElements.Count == 0)
                    {
                        gameObjectKeyFrame.gameMatched = true;
                    }
                }

                // for strict.. we needed to find all the expected key frame elements
                var allUIElementsFound = true;
                if (myReplayEnforcementMode != UIReplayEnforcement.Delta)
                {
                    allUIElementsFound = uiElements?.All(a => a.Value.Item2 == 0) ?? true;
                }
                else
                {
                    if (uiObjectKeyFrame != null  && uiElements != null)
                    {
                        foreach (var stateElementDeltaType in uiObjectKeyFrame.uiElementsDeltas)
                        {
                            var deltaHash = stateElementDeltaType.Key;
                            if (uiElements.TryGetValue(deltaHash, out var theVal))
                            {
                                if (stateElementDeltaType.Value == StateElementDeltaType.Decreased)
                                {
                                    if (theVal.Item2 < 0)
                                    {
                                        if (keyFrameCheckCount < KeyFrameChecksBeforePrompting)
                                        {
                                            return null;
                                        }
                                        // was more than before
                                        return gameElementsErrorMessage + uiObjectKeyFrame.tickNumber + " Wait for KeyFrame - Too many instances of UIElement:\r\n" + stateElementDeltaType.Key;
                                    }
                                }
                                else if (stateElementDeltaType.Value == StateElementDeltaType.Increased)
                                {
                                    if (theVal.Item2 > 0)
                                    {
                                        if (keyFrameCheckCount < KeyFrameChecksBeforePrompting)
                                        {
                                            return null;
                                        }
                                        // was the same or less than before
                                        return gameElementsErrorMessage + uiObjectKeyFrame.tickNumber + " Wait for KeyFrame - Too few instances of UIElement:\r\n" + stateElementDeltaType.Key;
                                    }
                                }
                                else if (stateElementDeltaType.Value == StateElementDeltaType.Zero)
                                {
                                    if (theVal.Item2 != 0)
                                    {
                                        if (keyFrameCheckCount < KeyFrameChecksBeforePrompting)
                                        {
                                            return null;
                                        }
                                        return gameElementsErrorMessage + uiObjectKeyFrame.tickNumber + " Wait for KeyFrame - Unexpected UIElement:\r\n" + stateElementDeltaType.Key;
                                    }
                                }
                                else // non-zero
                                {
                                    // make sure we found at least 1
                                    if (uiObjectKeyFrame.uiElementsCounts[deltaHash].Item2 - theVal.Item2 <= 0)
                                    {
                                        if (keyFrameCheckCount < KeyFrameChecksBeforePrompting)
                                        {
                                            return null;
                                        }
                                        return gameElementsErrorMessage + uiObjectKeyFrame.tickNumber + " Wait for KeyFrame - Too few instances of UIElement:\r\n" + stateElementDeltaType.Key;
                                    }
                                }

                            }
                            else
                            {
                                // we didn't find the keyframe entry in the current state
                                if (stateElementDeltaType.Value != StateElementDeltaType.Zero)
                                {
                                    if (keyFrameCheckCount < KeyFrameChecksBeforePrompting)
                                    {
                                        return null;
                                    }
                                    return gameElementsErrorMessage + uiObjectKeyFrame.tickNumber + " Wait for KeyFrame - Missing expected UIElement:\r\n" + stateElementDeltaType.Key;
                                }
                            }
                        }
                    }
                }

                if (uiObjectKeyFrame != null && (uiScenes == null || uiScenes.Count == 0) && (uiElements == null || allUIElementsFound))
                {
                    if (uiObjectKeyFrame.keyFrameTypes.Contains(KeyFrameType.UI_PIXELHASH) && uiObjectKeyFrame.pixelHash != null)
                    {
                        if (uiObjectKeyFrame.pixelHash != pixelHash)
                        {
                            // only bail out here if we're the ui is the oldest awaited key frame
                            if (eldestKeyFrame.tickNumber == uiObjectKeyFrame.tickNumber)
                            {
                                if (keyFrameCheckCount < KeyFrameChecksBeforePrompting)
                                {
                                    return null;
                                }
                                return gameElementsErrorMessage + uiObjectKeyFrame.tickNumber + " Wait for KeyFrame - PixelHash '"+ pixelHash +"' does not match expected '" + uiObjectKeyFrame.pixelHash +"'";
                            }
                        }
                        else
                        {
                            uiObjectKeyFrame.uiMatched = true;
                        }
                    }
                    else
                    {
                        uiObjectKeyFrame.uiMatched = true;
                    }
                }

                if (uiObjectKeyFrame?.uiMatched != true || gameElementsErrorMessage != null)
                {
                    if (keyFrameCheckCount < KeyFrameChecksBeforePrompting)
                    {
                        return null;
                    }
                    var missingConditions = "" + uiObjectKeyFrame?.tickNumber + ":"+ gameObjectKeyFrame?.tickNumber + " Wait for KeyFrame - Waiting for conditions...\r\nuiScenes:\r\n" + (uiScenes != null ? string.Join("\r\n", uiScenes):null) + "\r\nuiElements:\r\n" + (uiElements != null ? string.Join("\r\n", uiElements.Keys) : null) + "\r\ngameScenes:\r\n" + (gameScenes != null ? string.Join("\r\n", gameScenes) : null) + "\r\ngameElements:\r\n" + (gameElements!= null ? string.Join("\r\n", gameElements.Select(a => a.Item1)):null);
                    // still missing something from the key frame
                    return missingConditions;
                }
            }

            return null;
        }

        private enum KeyState
        {
            Up,
            Down
        }

        private void PlayInputs(Dictionary<int, RecordedGameObjectState> priorStates, Dictionary<int, RecordedGameObjectState> objectStates)
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
                        SendMouseEvent(replayMouseInputEntry.tickNumber, replayMouseInputEntry, priorStates, objectStates);
                        replayMouseInputEntry.IsDone = true;
                    }
                }
            }

            // clean out any inputs that are done
            _keyboardQueue.RemoveAll(a => a.IsDone);
            _mouseQueue.RemoveAll(a => a.IsDone);
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private void SendKeyEventLegacy(long tickNumber, Key key, KeyState upOrDown)
        {
            if (RGLegacyInputWrapper.IsPassthrough)
            {
                // simulation not started
                return;
            }

            switch (upOrDown)
            {
                case KeyState.Down:
                    RGLegacyInputWrapper.SimulateKeyPress(RGLegacyInputUtils.InputSystemKeyToKeyCode(key));
                    break;
                case KeyState.Up:
                    RGLegacyInputWrapper.SimulateKeyRelease(RGLegacyInputUtils.InputSystemKeyToKeyCode(key));
                    break;
                default:
                    RGDebug.LogError($"Unexpected key state {upOrDown}");
                    break;
            }
        }
#endif

        private void SendKeyEvent(long tickNumber, Key key, KeyState upOrDown)
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            SendKeyEventLegacy(tickNumber, key, upOrDown);
            #endif
            
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
                        var value = _dataContainer.IsShiftDown ? possibleValues.Item2 : possibleValues.Item1;
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

            if (!mouse.enabled)
            {
                InputSystem.EnableDevice(mouse);
            }

            if (forceListener || created)
            {
                _mouseEventHandler = InputSystem.onEvent.ForDevice(mouse).Call(e =>
                {
                    var positionControl = mouse.allControls.First(a => a is Vector2Control && a.name == "position") as Vector2Control;
                    var position = positionControl.ReadValueFromEvent(e);

                    var buttonsClicked = mouse.allControls.FirstOrDefault(a =>
                        a is ButtonControl abc && abc.ReadValueFromEvent(e) > 0.1f
                    ) != null;
                    RGDebug.LogDebug("Mouse event at: " + position.x + "," + position.y + "  buttonsClicked: " + buttonsClicked);
                    // need to use the static accessor here as this anonymous function's parent gameObject instance could get destroyed
                    FindObjectOfType<VirtualMouseCursor>().SetPosition(position, buttonsClicked);
                });
            }

            return mouse;
        }

        public class RecordedGameObjectStatePathEqualityComparer : IEqualityComparer<RecordedGameObjectState>
        {
            public bool Equals(RecordedGameObjectState x, RecordedGameObjectState y)
            {
                if (x?.worldSpaceBounds != null || y?.worldSpaceBounds != null)
                {
                    // for world space objects, we don't want to unique-ify based on path
                    return false;
                }
                return x?.path == y?.path;
            }

            public int GetHashCode(RecordedGameObjectState obj)
            {
                return obj.path.GetHashCode();
            }
        }

        // ReSharper disable once InconsistentNaming
        private static readonly RecordedGameObjectStatePathEqualityComparer _recordedGameObjectStatePathEqualityComparer = new();

        private static Vector2? _lastMousePosition;

        // Finds the best object to adjust our click position to for a given mouse input
        // Uses the exact path for UI clicks, but the normalized path for world space clicks
        // Returns (the object, whether it was world space, the suggested mouse position, and the states list as a convenience)
        private static (RecordedGameObjectState, bool, Vector2, IEnumerable<RecordedGameObjectState>) FindBestClickObject(Camera mainCamera, long tickNumber, ReplayMouseInputEntry mouseInput, Dictionary<int, RecordedGameObjectState> priorStates, Dictionary<int, RecordedGameObjectState> objectStates)
        {

            // Mouse is hard... we can't use the raw position, we need to use the position relative to the current resolution
            // but.. it gets tougher than that.  Some UI elements scale differently with resolution (only horizontal, only vertical, preserve aspect, expand, etc)
            // so we have to take the bounding space of the original object(s) clicked on into consideration
            var normalizedPosition = mouseInput.NormalizedPosition;

            // note that if this mouse input wasn't a click, there will be no possible objects
            if (mouseInput.clickedObjectPaths == null || mouseInput.clickedObjectPaths.Length == 0)
            {
                // bail out early, no click
                return (null, false, normalizedPosition, objectStates.Values);
            }

            var theNp = new Vector3(normalizedPosition.x, normalizedPosition.y, 0f);

            var originalPosition = mouseInput.position;

            // find possible objects prioritizing the currentState, falling back to the priorState

            // copy so we can remove
            var mousePaths = mouseInput.worldPosition != null ? mouseInput.clickedObjectNormalizedPaths : mouseInput.clickedObjectPaths;
            var pathsToFind = mousePaths.ToList();

            var possibleObjects = new List<RecordedGameObjectState>();

            foreach (var os in objectStates.Values)
            {
                if (mouseInput.worldPosition != null)
                {
                    if (StateRecorderUtils.OptimizedContainsStringInArray(mousePaths, os.normalizedPath))
                    {
                        possibleObjects.Add(os);
                        StateRecorderUtils.OptimizedRemoveStringFromList(pathsToFind, os.normalizedPath);
                    }
                }
                else
                {
                    if (StateRecorderUtils.OptimizedContainsStringInArray(mousePaths, os.path))
                    {
                        possibleObjects.Add(os);
                        StateRecorderUtils.OptimizedRemoveStringFromList(pathsToFind, os.path);
                    }
                }
            }

            // still have some objects we didnt' find in the current state, check previous state
            if (pathsToFind.Count > 0)
            {
                foreach (var os in objectStates.Values)
                {
                    if (mouseInput.worldPosition != null)
                    {
                        if (StateRecorderUtils.OptimizedContainsStringInList(pathsToFind, os.normalizedPath))
                        {
                            possibleObjects.Add(os);
                        }
                    }
                    else
                    {
                        if (StateRecorderUtils.OptimizedContainsStringInList(pathsToFind, os.path))
                        {
                            possibleObjects.Add(os);
                        }
                    }
                }
            }

            var cos = possibleObjects.OrderBy(a =>
            {
                var closestPointInA = a.screenSpaceBounds.ClosestPoint(theNp);
                return (theNp - closestPointInA).sqrMagnitude;
            });
            possibleObjects = cos.Distinct(_recordedGameObjectStatePathEqualityComparer)
                .ToList(); // select only the first entry of each path for ui elements; uses ToList due to multiple iterations of this structure later in the code to avoid multiple enumeration

            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            float smallestSize = screenWidth * screenHeight;

            RecordedGameObjectState bestObject = null;
            var worldSpaceObject = false;

            var possibleObjectsCount = possibleObjects.Count;
            for (var j = 0; j < possibleObjectsCount; j++)
            {
                var objectToCheck = possibleObjects[j];
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
                            // uses the collider bounds on that object as colliders are what would drive world space objects' click detection in scripts / etc
                            if ((objectToCheck.colliders.Count == 0 && objectToCheck.worldSpaceBounds.Value.Contains(mouseWorldPosition)) || objectToCheck.colliders.FirstOrDefault(a => a.collider.bounds.Contains(mouseWorldPosition)) != null)
                            {
                                var screenPoint = mainCamera.WorldToScreenPoint(mouseWorldPosition);
                                if (screenPoint.x < 0 || screenPoint.x > screenWidth || screenPoint.y < 0 || screenPoint.y > screenHeight)
                                {
                                    RGDebug.LogWarning($"Attempted to click at worldPosition: [{mouseWorldPosition.x},{mouseWorldPosition.y},{mouseWorldPosition.z}], which is off screen at position: [{screenPoint.x},{screenPoint.y}]");
                                }
                                else
                                {
                                    bestObject = objectToCheck;
                                    worldSpaceObject = true;
                                    var old = normalizedPosition;
                                    // we hit one of our world objects, set the normalized position and stop looping
                                    normalizedPosition = new Vector2Int((int)screenPoint.x, (int)screenPoint.y);
                                    RGDebug.LogInfo($"({tickNumber}) Adjusting world click location to ensure hit on object: " + objectToCheck.path + " oldPosition: (" + old.x + "," + old.y + "), newPosition: (" + normalizedPosition.x + "," + normalizedPosition.y + ")");
                                    break; // end the for
                                }
                            }
                            else
                            {
                                // didn't hit a collider on this object, we fall back to the renderer bounds bestObject evaluation method, similar to ui elements

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
                            // mouse input didn't capture a world position, so we weren't clicking on a world space object.. ignore anything not UI related from consideration
                            if (objectToCheck.worldSpaceBounds == null)
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

            return (bestObject, worldSpaceObject, normalizedPosition, possibleObjects);
        }

        public static void SendMouseEvent(long tickNumber, ReplayMouseInputEntry mouseInput, Dictionary<int, RecordedGameObjectState> priorStates, Dictionary<int, RecordedGameObjectState> objectStates)
        {
            var clickObjectResult = FindBestClickObject(Camera.main, tickNumber, mouseInput, priorStates, objectStates);

            var bestObject = clickObjectResult.Item1;
            var normalizedPosition = clickObjectResult.Item3;

            // non-world space object found, make sure we hit the object
            if (bestObject != null && !clickObjectResult.Item2)
            {
                var clickBounds = bestObject.screenSpaceBounds;

                var possibleObjects = clickObjectResult.Item4;
                foreach(var objectToCheck in possibleObjects)
                {
                    if (clickBounds.Intersects(objectToCheck.screenSpaceBounds))
                    {
                        // max of the mins; and min of the maxes
                        clickBounds.SetMinMax(
                            Vector3.Max(clickBounds.min, objectToCheck.screenSpaceBounds.min),
                            Vector3.Min(clickBounds.max, objectToCheck.screenSpaceBounds.max)
                        );
                    }
                }

                // make sure our click is on that object
                if (!clickBounds.Contains(normalizedPosition))
                {
                    var old = normalizedPosition;
                    // use the center of these bounds as our best point to click
                    normalizedPosition = new Vector2((int)clickBounds.center.x, (int)clickBounds.center.y);

                    RGDebug.LogInfo($"({tickNumber}) Adjusting click location to ensure hit on object path: " + bestObject.path + " oldPosition: (" + old.x + "," + old.y + "), newPosition: (" + normalizedPosition.x + "," + normalizedPosition.y + ")");


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
                    switch (mouseControl.name)
                    {
                        case "delta":
                            if (_lastMousePosition != null)
                            {
                                var delta = normalizedPosition - _lastMousePosition.Value;
                                if (RGDebug.IsDebugEnabled)
                                {
                                    mouseEventString += $"delta: {delta.x},{delta.y}  ";
                                }

                                ((Vector2Control)mouseControl).WriteValueIntoEvent(delta, eventPtr);
                            }
                            break;
                        case "position":
                            if (RGDebug.IsDebugEnabled)
                            {
                                mouseEventString += $"position: {normalizedPosition.x},{normalizedPosition.y}  ";
                            }

                            ((Vector2Control)mouseControl).WriteValueIntoEvent(normalizedPosition, eventPtr);
                            break;
                        case "scroll":
                            if (RGDebug.IsDebugEnabled)
                            {
                                if (mouseInput.scroll.x < -0.1f || mouseInput.scroll.x > 0.1f || mouseInput.scroll.y < -0.1f || mouseInput.scroll.y > 0.1f)
                                {
                                    mouseEventString += $"scroll: {mouseInput.scroll.x},{mouseInput.scroll.y}  ";
                                }
                            }

                            ((DeltaControl)mouseControl).WriteValueIntoEvent(mouseInput.scroll, eventPtr);
                            break;
                        case "leftButton":
                            if (RGDebug.IsDebugEnabled)
                            {
                                if (mouseInput.leftButton)
                                {
                                    mouseEventString += $"leftButton  ";
                                }
                            }

                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.leftButton ? 1f : 0f, eventPtr);
                            break;
                        case "middleButton":
                            if (RGDebug.IsDebugEnabled)
                            {
                                if (mouseInput.middleButton)
                                {
                                    mouseEventString += $"middleButton  ";
                                }
                            }

                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.middleButton ? 1f : 0f, eventPtr);
                            break;
                        case "rightButton":
                            if (RGDebug.IsDebugEnabled)
                            {
                                if (mouseInput.rightButton)
                                {
                                    mouseEventString += $"rightButton  ";
                                }
                            }

                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.rightButton ? 1f : 0f, eventPtr);
                            break;
                        case "forwardButton":
                            if (RGDebug.IsDebugEnabled)
                            {
                                if (mouseInput.forwardButton)
                                {
                                    mouseEventString += $"forwardButton  ";
                                }
                            }

                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.forwardButton ? 1f : 0f, eventPtr);
                            break;
                        case "backButton":
                            if (RGDebug.IsDebugEnabled)
                            {
                                if (mouseInput.backButton)
                                {
                                    mouseEventString += $"backButton  ";
                                }
                            }

                            ((ButtonControl)mouseControl).WriteValueIntoEvent(mouseInput.backButton ? 1f : 0f, eventPtr);
                            break;
                    }
                }
                _lastMousePosition = normalizedPosition;

                if (RGDebug.IsDebugEnabled)
                {
                    RGDebug.LogDebug($"({tickNumber}) Sending Mouse Event - {mouseEventString}");
                }

                InputSystem.QueueEvent(eventPtr);
            }
        }

        public void Update()
        {
            if (_dataContainer != null)
            {
                if (_startPlaying)
                {
                    #if ENABLE_LEGACY_INPUT_MANAGER
                    RGLegacyInputWrapper.StartSimulation(this);
                    #endif
                    _lastStartTime = Time.unscaledTime;
                    _priorKeyFrameTime = null;
                    _startPlaying = false;
                    _isPlaying = true;
                    _nextKeyFrames.Add(_dataContainer.DequeueKeyFrame());
                    // if starting to play, or on loop 1.. start recording
                    if (_loopCount < 2)
                    {
                        _screenRecorder.StartRecording(_dataContainer.SessionId);
                    }
                }

                if (_isPlaying)
                {
                    var states = InGameObjectFinder.GetInstance()?.GetStateForCurrentFrame(true);
                    var priorStates = states?.Item1;
                    var currentStates = states?.Item2;

                    var gameFacePixelHashObserver = GameFacePixelHashObserver.GetInstance();
                    string pixelHash = null;
                    if (gameFacePixelHashObserver != null)
                    {
                        gameFacePixelHashObserver.SetActive(true);
                        // don't clear the value on read during playback
                        pixelHash = gameFacePixelHashObserver.GetPixelHash(false);

                    }
                    CheckWaitForKeyStateMatch(currentStates, pixelHash);

                    PlayInputs(priorStates, currentStates);
                }
            }

            if (_isPlaying && _nextKeyFrames.Count == 0 && _keyboardQueue.Count == 0 && _mouseQueue.Count == 0)
            {
                SendMouseEvent(0, new ReplayMouseInputEntry()
                {
                    // get the mouse off the screen, when replay fails, we leave the virtual mouse cursor alone so they can see its location at time of failure
                    position = new Vector2Int(Screen.width + 20, -20)
                }, null, ScreenRecorder._emptyStateDictionary);

                // we hit the end of the replay
                if (_loopCount > -1)
                {
                    ResetForLooping();
                    _loopCountCallback.Invoke(++_loopCount);
                }
                else
                {
                    Reset();
                    _replaySuccessful = true;
                }
            }
        }
    }
}
