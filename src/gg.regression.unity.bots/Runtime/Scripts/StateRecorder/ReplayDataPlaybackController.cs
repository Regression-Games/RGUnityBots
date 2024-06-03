﻿using System;
using System.Collections.Generic;
using System.Linq;
using StateRecorder;
using TMPro;
using UnityEditor.AnimatedValues;
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

        private ReplayBotSegmentsContainer _dataContainer;

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
        private readonly List<BotSegmment> _nextBotSegments = new();

        private double _lastInputQueueStartTime = -1;
        private double CurrentInputTimePoint => Time.unscaledTime - _lastInputQueueStartTime;
        private readonly List<KeyboardInputActionData> _keyboardQueue = new();
        private readonly List<MouseInputActionData> _mouseQueue = new();

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

        public void SetDataContainer(ReplayBotSegmentsContainer dataContainer)
        {
            Stop();
            _replaySuccessful = null;

            SendMouseEvent(new MouseInputActionData()
            {
                // get the mouse off the screen, when replay fails, we leave the virtual mouse cursor alone so they can see its location at time of failure, but on new file, we want this gone
                position = new Vector2Int(Screen.width +20, -20)
            }, ScreenRecorder._emptyTransformStatusDictionary, ScreenRecorder._emptyTransformStatusDictionary, ScreenRecorder._emptyTransformStatusDictionary, ScreenRecorder._emptyTransformStatusDictionary);

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
            _nextBotSegments.Clear();
            _startPlaying = false;
            _isPlaying = false;
            _loopCount = -1;
            _replaySuccessful = null;
            WaitingForKeyFrameConditions = null;

            _screenRecorder.StopRecording();

            _dataContainer = null;
            InGameObjectFinder.GetInstance()?.Cleanup();
        }

        public void Reset()
        {
            _nextBotSegments.Clear();
            _startPlaying = false;
            _isPlaying = false;
            _loopCount = -1;
            _replaySuccessful = null;
            WaitingForKeyFrameConditions = null;

            _screenRecorder.StopRecording();

            // similar to Stop, but assumes will play again
            _dataContainer?.Reset();

            InGameObjectFinder.GetInstance()?.Cleanup();
        }

        public void ResetForLooping()
        {
            _nextBotSegments.Clear();
            _startPlaying = true;
            _isPlaying = false;
            // don't change _loopCount
            _replaySuccessful = null;
            WaitingForKeyFrameConditions = null;

            // similar to Stop, but assumes continued looping .. doesn't stop recording
            _dataContainer?.Reset();

            InGameObjectFinder.GetInstance()?.Cleanup();
        }

        public bool IsPlaying()
        {
            return _isPlaying;
        }

        private enum KeyState
        {
            Up,
            Down
        }
        private void PlayInputs()
        {
            var currentTime = CurrentInputTimePoint;
            if (_keyboardQueue.Count > 0)
            {
                foreach (var replayKeyboardInputEntry in _keyboardQueue)
                {
                    if (!replayKeyboardInputEntry.Replay_StartEndSentFlags[0] && currentTime >= replayKeyboardInputEntry.startTime)
                    {
                        // send start event
                        SendKeyEvent(replayKeyboardInputEntry.Key, KeyState.Down);
                        replayKeyboardInputEntry.Replay_StartEndSentFlags[0] = true;
                    }

                    if (!replayKeyboardInputEntry.Replay_StartEndSentFlags[1] && currentTime >= replayKeyboardInputEntry.endTime)
                    {
                        // send end event
                        SendKeyEvent(replayKeyboardInputEntry.Key, KeyState.Up);
                        replayKeyboardInputEntry.Replay_StartEndSentFlags[1] = true;
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
                        SendMouseEvent(replayMouseInputEntry, ScreenRecorder._emptyTransformStatusDictionary, ScreenRecorder._emptyTransformStatusDictionary, ScreenRecorder._emptyTransformStatusDictionary, ScreenRecorder._emptyTransformStatusDictionary);
                        replayMouseInputEntry.Replay_IsDone = true;
                    }
                }
            }

            // clean out any inputs that are done
            _keyboardQueue.RemoveAll(a => a.Replay_IsDone);
            _mouseQueue.RemoveAll(a => a.Replay_IsDone);
        }

        private void SendKeyEvent(Key key, KeyState upOrDown)
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

                var inputControl = keyboard.allControls
                    .FirstOrDefault(a => a is KeyControl kc && kc.keyCode == key) ?? keyboard.anyKey;

                if (inputControl != null)
                {
                    RGDebug.LogInfo($"Sending Key Event: {key} - {upOrDown}");

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

        public class RecordedGameObjectStatePathEqualityComparer : IEqualityComparer<TransformStatus>
        {
            public bool Equals(TransformStatus x, TransformStatus y)
            {
                if (x?.worldSpaceBounds != null || y?.worldSpaceBounds != null)
                {
                    // for world space objects, we don't want to unique-ify based on path
                    return false;
                }
                return x?.Path == y?.Path;
            }

            public int GetHashCode(TransformStatus obj)
            {
                return obj.Path.GetHashCode();
            }
        }

        // ReSharper disable once InconsistentNaming
        private static readonly RecordedGameObjectStatePathEqualityComparer _recordedGameObjectStatePathEqualityComparer = new();

        private static Vector2? _lastMousePosition;

        // Finds the best object to adjust our click position to for a given mouse input
        // Uses the exact path for UI clicks, but the normalized path for world space clicks
        // Returns (the object, whether it was world space, the suggested mouse position)
        private static (TransformStatus, bool, Vector2, IEnumerable<TransformStatus>) FindBestClickObject(Camera mainCamera, MouseInputActionData mouseInput, Dictionary<int, TransformStatus> priorUiTransforms, Dictionary<int, TransformStatus> priorGameObjectTransforms, Dictionary<int, TransformStatus> uiTransforms, Dictionary<int, TransformStatus> gameObjectTransforms)
        {

            // Mouse is hard... we can't use the raw position, we need to use the position relative to the current resolution
            // but.. it gets tougher than that.  Some UI elements scale differently with resolution (only horizontal, only vertical, preserve aspect, expand, etc)
            // so we have to take the bounding space of the original object(s) clicked on into consideration
            var normalizedPosition = mouseInput.NormalizedPosition;

            // note that if this mouse input wasn't a click, there will be no possible objects
            if (mouseInput.clickedObjectNormalizedPaths == null || mouseInput.clickedObjectNormalizedPaths.Length == 0)
            {
                // bail out early, no click
                return (null, false, normalizedPosition, uiTransforms.Values.Concat(gameObjectTransforms.Values));
            }

            var theNp = new Vector3(normalizedPosition.x, normalizedPosition.y, 0f);

            // find possible objects prioritizing the currentState, falling back to the priorState

            // copy so we can remove
            var mousePaths = mouseInput.clickedObjectNormalizedPaths;
            var pathsToFind = mousePaths.ToList();

            var possibleObjects = new List<TransformStatus>();

            foreach (var os in uiTransforms)
            {
                var val = os.Value;
                if (StateRecorderUtils.OptimizedContainsStringInArray(mousePaths, val.Path))
                {
                    possibleObjects.Add(val);
                    StateRecorderUtils.OptimizedRemoveStringFromList(pathsToFind, val.Path);
                }
            }

            foreach (var os in gameObjectTransforms)
            {
                var val = os.Value;
                if (StateRecorderUtils.OptimizedContainsStringInArray(mousePaths, val.NormalizedPath))
                {
                    possibleObjects.Add(val);
                    StateRecorderUtils.OptimizedRemoveStringFromList(pathsToFind, val.NormalizedPath);
                }
            }


            // still have some objects we didnt' find in the current state, check previous state
            // this is used primarly for mouse up event processing
            if (pathsToFind.Count > 0)
            {
                foreach (var os in priorUiTransforms)
                {
                    var val = os.Value;
                    if (StateRecorderUtils.OptimizedContainsStringInArray(mousePaths, val.Path))
                    {
                        possibleObjects.Add(val);
                    }
                }

                foreach (var os in priorGameObjectTransforms)
                {
                    var val = os.Value;
                    if (StateRecorderUtils.OptimizedContainsStringInArray(mousePaths, val.NormalizedPath))
                    {
                        possibleObjects.Add(val);
                    }
                }
            }

            var cos = possibleObjects.Where(a=>a.screenSpaceBounds.HasValue).OrderBy(a =>
            {
                var closestPointInA = a.screenSpaceBounds.Value.ClosestPoint(theNp);
                return (theNp - closestPointInA).sqrMagnitude;
            });
            possibleObjects = cos.Distinct(_recordedGameObjectStatePathEqualityComparer)
                .ToList(); // select only the first entry of each path for ui elements; uses ToList due to multiple iterations of this structure later in the code to avoid multiple enumeration

            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            float smallestSize = screenWidth * screenHeight;

            TransformStatus bestObject = null;
            var worldSpaceObject = false;

            var possibleObjectsCount = possibleObjects.Count;
            for (var j = 0; j < possibleObjectsCount; j++)
            {
                var objectToCheck = possibleObjects[j];
                var ssb = objectToCheck.screenSpaceBounds.Value;
                var size = ssb.size.x * ssb.size.y;

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
                            var colliders = objectToCheck.Transform.GetComponentsInChildren<Collider>();
                            if ((colliders.Length == 0 && objectToCheck.worldSpaceBounds.Value.Contains(mouseWorldPosition)) || colliders.FirstOrDefault(a => a.bounds.Contains(mouseWorldPosition)) != null)
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
                                    RGDebug.LogInfo($"Adjusting world click location to ensure hit on object: " + objectToCheck.Path + " oldPosition: (" + old.x + "," + old.y + "), newPosition: (" + normalizedPosition.x + "," + normalizedPosition.y + ")");
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

        public static void SendMouseEvent(MouseInputActionData mouseInput, Dictionary<int, TransformStatus> priorUiTransforms, Dictionary<int, TransformStatus> priorGameObjectTransforms, Dictionary<int, TransformStatus> uiTransforms, Dictionary<int, TransformStatus> gameObjectTransforms)
        {
            var clickObjectResult = FindBestClickObject(Camera.main, mouseInput, priorUiTransforms, priorGameObjectTransforms,uiTransforms, gameObjectTransforms);

            var bestObject = clickObjectResult.Item1;
            var normalizedPosition = clickObjectResult.Item3;

            // non-world space object found, make sure we hit the object
            if (bestObject != null && bestObject.screenSpaceBounds.HasValue && !clickObjectResult.Item2)
            {
                var clickBounds = bestObject.screenSpaceBounds.Value;

                var possibleObjects = clickObjectResult.Item4;
                foreach (var objectToCheck in possibleObjects)
                {
                    if (objectToCheck.screenSpaceBounds.HasValue)
                    {
                        var ssb = objectToCheck.screenSpaceBounds.Value;
                        if (clickBounds.Intersects(ssb))
                        {
                            // max of the mins; and min of the maxes
                            clickBounds.SetMinMax(
                                Vector3.Max(clickBounds.min, ssb.min),
                                Vector3.Min(clickBounds.max, ssb.max)
                            );
                        }
                    }
                }

                // make sure our click is on that object
                if (!clickBounds.Contains(normalizedPosition))
                {
                    var old = normalizedPosition;
                    // use the center of these bounds as our best point to click
                    normalizedPosition = new Vector2((int)clickBounds.center.x, (int)clickBounds.center.y);

                    RGDebug.LogInfo($"Adjusting click location to ensure hit on object path: " + bestObject.Path + " oldPosition: (" + old.x + "," + old.y + "), newPosition: (" + normalizedPosition.x + "," + normalizedPosition.y + ")");
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
                    RGDebug.LogDebug($"Sending Mouse Event - {mouseEventString}");
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
                    _lastStartTime = Time.unscaledTime;
                    _priorKeyFrameTime = null;
                    _startPlaying = false;
                    _isPlaying = true;
                    _nextBotSegments.Add(_dataContainer.DequeueBotSegment());
                    // if starting to play, or on loop 1.. start recording
                    if (_loopCount < 2)
                    {
                        _screenRecorder.StartRecording(_dataContainer.SessionId);
                    }
                }
            }
        }

        private void EvaluateBotSegments()
        {
            // check count each loop because we remove from it during the loop
            for (var i = 0; i < _nextBotSegments.Count; /* do not increment here*/)
            {
                var nextBotSegment = _nextBotSegments[i];
                var matched = KeyFrameEvaluator.Evaluator.Matched(nextBotSegment.keyFrameCriteria);
                if (matched)
                {
                    //Process the inputs from that bot segment if necessary
                    var botAction = nextBotSegment.botAction;
                    if (botAction != null)
                    {
                        if (botAction.data is InputPlaybackActionData ipad)
                        {
                            // record NOW... and offset the input data times based on their frame start time relative to now
                            _lastInputQueueStartTime = ipad.startTime;
                            _keyboardQueue.AddRange(ipad.inputData.keyboard);
                            _mouseQueue.AddRange(ipad.inputData.mouse);
                        }
                    }
                    _nextBotSegments.RemoveAt(0);
                }
            }

            // see if the last entry has transient matches.. if so.. dequeue another
            if (_nextBotSegments.Count > 0)
            {
                if (_nextBotSegments[^1].Replay_TransientMatched)
                {
                    var next = _dataContainer.DequeueBotSegment();
                    if (next != null)
                    {
                        _nextBotSegments.Add(next);
                    }
                }
            }
            else
            {
                var next = _dataContainer.DequeueBotSegment();
                if (next != null)
                {
                    _nextBotSegments.Add(next);
                }
            }
        }

        public void LateUpdate()
        {
            if (_isPlaying)
            {
                if (_dataContainer != null)
                {
                    EvaluateBotSegments();
                    PlayInputs();
                }

                if (_nextBotSegments.Count == 0)
                {

                    SendMouseEvent(new MouseInputActionData()
                    {
                        // get the mouse off the screen, when replay fails, we leave the virtual mouse cursor alone so they can see its location at time of failure
                        position = new Vector2Int(Screen.width + 20, -20)
                    }, ScreenRecorder._emptyTransformStatusDictionary, ScreenRecorder._emptyTransformStatusDictionary, ScreenRecorder._emptyTransformStatusDictionary, ScreenRecorder._emptyTransformStatusDictionary);

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
}
