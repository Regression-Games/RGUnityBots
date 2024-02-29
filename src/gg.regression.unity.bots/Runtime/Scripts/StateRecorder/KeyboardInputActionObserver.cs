using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.StateRecorder
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class KeyboardInputActionData
    {
        public double startTime;
        public string action;
        public string binding;
        public double? endTime;
        [NonSerialized] public double duration;
        [NonSerialized] public double? lastSentUpdateTime;
        [NonSerialized] public double lastUpdateTime;
        public bool isPressed => duration > 0 && endTime == null;
    }

    public class KeyboardInputActionObserver : MonoBehaviour
    {
        private static KeyboardInputActionObserver _this;

        [Tooltip("Used on OSX runtimes to convert spammy keyboard events into proper key held states")]
        public float keyHeldThresholdSeconds = 0.100f;

        private readonly ConcurrentDictionary<string, KeyboardInputActionData> _activeInputActions = new();
        private readonly ConcurrentQueue<KeyboardInputActionData> _completedInputActions = new();

        private InputActionAsset _inputActionAsset;

        private bool _recording;

        public static readonly IReadOnlyDictionary<string, Key> AllKeyboardKeys = new ReadOnlyDictionary<string, Key> (new Dictionary<string, Key>()
        {
            //row 1 (top row)
            {"escape", Key.Escape},
            {"f1", Key.F1},
            {"f2", Key.F2},
            {"f3", Key.F3},
            {"f4", Key.F4},
            {"f5", Key.F5},
            {"f6", Key.F6},
            {"f7", Key.F7},
            {"f8", Key.F8},
            {"f9", Key.F9},
            {"f10", Key.F10},
            {"f11", Key.F11},
            {"f12", Key.F12},
            {"printScreen", Key.PrintScreen},
            {"scrollLock", Key.ScrollLock},
            {"pause", Key.Pause},

            // row 2
            {"backquote", Key.Backquote},
            {"1", Key.Digit1},
            {"2", Key.Digit2},
            {"3", Key.Digit3},
            {"4", Key.Digit4},
            {"5", Key.Digit5},
            {"6", Key.Digit6},
            {"7", Key.Digit7},
            {"8", Key.Digit8},
            {"9", Key.Digit9},
            {"0", Key.Digit0},
            {"minus", Key.Minus},
            {"equals", Key.Equals},
            {"backspace", Key.Backspace},
            {"insert", Key.Insert},
            {"home", Key.Home},
            {"pageUp", Key.PageUp},
            {"numLock", Key.NumLock},
            {"numpadDivide", Key.NumpadDivide},
            {"numpadMultiply", Key.NumpadMultiply},
            {"numpadMinus", Key.NumpadMinus},

            //row 3
            {"tab", Key.Tab},
            {"q", Key.Q},
            {"w", Key.W},
            {"e", Key.E},
            {"r", Key.R},
            {"t", Key.T},
            {"y", Key.Y},
            {"u", Key.U},
            {"i", Key.I},
            {"o", Key.O},
            {"p", Key.P},
            {"leftBracket", Key.LeftBracket},
            {"rightBracket", Key.RightBracket},
            {"backSlash", Key.Backslash},
            {"delete", Key.Delete},
            {"end", Key.End},
            {"pageDown", Key.PageDown},
            {"numpad7", Key.Numpad7},
            {"numpad8", Key.Numpad8},
            {"numpad9", Key.Numpad9},
            {"numpadPlus", Key.NumpadPlus},

            //row 4
            {"capsLock", Key.CapsLock},
            {"a", Key.A},
            {"s", Key.S},
            {"d", Key.D},
            {"f", Key.F},
            {"g", Key.G},
            {"h", Key.H},
            {"j", Key.J},
            {"k", Key.K},
            {"l", Key.L},
            {"semicolon", Key.Semicolon},
            {"quote", Key.Quote},
            {"enter", Key.Enter},
            {"numpad4", Key.Numpad4},
            {"numpad5", Key.Numpad5},
            {"numpad6", Key.Numpad6},
            // big plus button already in row 3

            //row 5
            {"leftShift", Key.LeftShift},
            {"z", Key.Z},
            {"x", Key.X},
            {"c", Key.C},
            {"v", Key.V},
            {"b", Key.B},
            {"n", Key.N},
            {"m", Key.M},
            {"comma", Key.Comma},
            {"period", Key.Period},
            {"slash", Key.Slash},
            {"rightShift", Key.RightShift},
            {"upArrow", Key.UpArrow},
            {"numpad1", Key.Numpad1},
            {"numpad2", Key.Numpad2},
            {"numpad3", Key.Numpad3},
            {"numpadEnter", Key.NumpadEnter},

            // row 6 (bottom row)
            {"leftCtrl", Key.LeftCtrl},
            {"leftMeta", Key.LeftMeta}, // windows Logo or
            {"leftAlt", Key.LeftAlt},
            {"space", Key.Space},
            {"rightAlt", Key.RightAlt},
            {"rightMeta", Key.RightMeta}, // windows Fn or m
            {"contextMenu", Key.ContextMenu},
            {"rightCtrl", Key.RightCtrl},
            {"leftArrow", Key.LeftArrow},
            {"downArrow", Key.DownArrow},
            {"rightArrow", Key.RightArrow},
            {"numpad0", Key.Numpad0},
            {"numpadPeriod", Key.NumpadPeriod},
            // big enter button already in row 5

            // Keyboard OEM Special keys
            {"OEM1", Key.OEM1},
            {"OEM2", Key.OEM2},
            {"OEM3", Key.OEM3},
            {"OEM4", Key.OEM4},
            {"OEM5", Key.OEM5},
        });

        public void Awake()
        {
            if (_this != null)
            {
                // only allow 1 of these to be alive
                if (_this.gameObject != gameObject)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            // keep this thing alive across scenes
            DontDestroyOnLoad(gameObject);
            _this = this;
        }

        private void Start()
        {
            //define action map
            var inputActionMap = new InputActionMap("RegressionGames");

            foreach (var keyboardKey in AllKeyboardKeys.Keys)
            {
                CreateKeyboardAction(inputActionMap, keyboardKey);
            }

            // setup control scheme
            var controlScheme = new InputControlScheme("RegressionKeyboardListener", new[]
            {
                new InputControlScheme.DeviceRequirement
                {
                    controlPath = Keyboard.current?.path,
                    isAND = false,
                    isOptional = true
                }
            });

            // setup the actionAsset
            _inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            _inputActionAsset.AddActionMap(inputActionMap);
            _inputActionAsset.AddControlScheme(controlScheme);
            _inputActionAsset.Enable();
        }

        private void OnEnable()
        {
            if (_inputActionAsset != null)
            {
                _inputActionAsset.Enable();
            }
        }

        private void OnDisable()
        {
            if (_inputActionAsset != null)
            {
                _inputActionAsset.Disable();
            }
        }

        public static KeyboardInputActionObserver GetInstance()
        {
            return _this;
        }

        public void StartRecording()
        {
            _activeInputActions.Clear();
            _recording = true;
        }

        public void StopRecording()
        {
            _recording = false;
        }

        private void CreateKeyboardAction(InputActionMap inputActionMap, string keyName)
        {
            var inputAction =
                inputActionMap.AddAction("Keyboard/" + keyName, InputActionType.Value, "<Keyboard>/" + keyName);
            inputAction.performed += ActionPerformed;
            inputAction.canceled += ActionCanceled;
        }

        private void ActionCanceled(InputAction.CallbackContext context)
        {
            if (_recording)
            {
                RGDebug.LogVerbose("ActionCanceled - " + context.action.name);
                // record the end time

                if (Application.platform == RuntimePlatform.OSXPlayer)
                {
                    if (_activeInputActions.TryGetValue(context.action.name, out var actionData))
                    {
                        RGDebug.LogVerbose("ActionCanceled - updating");
                        // don't set action.isPressed here, we set it before sending back
                        actionData.lastUpdateTime = context.time;
                        actionData.duration = context.time - actionData.startTime;
                    }
                }
                else
                {
                    // NOT a Mac.. works correctly (ie: Windows 11, haven't tested Linux)
                    if (_activeInputActions.TryRemove(context.action.name, out var actionData))
                    {
                        RGDebug.LogVerbose("ActionCanceled - end action");
                        actionData.lastUpdateTime = context.time;
                        actionData.duration = context.time - actionData.startTime;
                        actionData.endTime = context.time;
                        _completedInputActions.Enqueue(actionData);
                    }
                }
            }
        }

        private void ActionPerformed(InputAction.CallbackContext context)
        {
            if (_recording)
            {
                RGDebug.LogVerbose("ActionPerformed - " + context.action.name);
                var action = context.action;

                if (!_activeInputActions.TryGetValue(action.name, out var activeAction))
                {
                    RGDebug.LogVerbose("ActionPerformed - new action - " + action.name);
                    activeAction = new KeyboardInputActionData
                    {
                        action = action.name,
                        binding = action.bindings[0].path,
                        startTime = context.startTime,
                        lastUpdateTime = context.time,
                        duration = context.time - context.startTime
                    };
                    _activeInputActions[action.name] = activeAction;
                }
                else
                {
                    if (Application.platform == RuntimePlatform.OSXPlayer)
                    {
                        // see if there is an existing one that is 'too old' or not and do updates accordingly
                        // too old.. finish it up and put into the completed queue
                        if (context.time - activeAction.lastUpdateTime > keyHeldThresholdSeconds)
                        {
                            // this is a new press, not a hold
                            if (_activeInputActions.TryRemove(action.name, out var oldAction))
                            {
                                RGDebug.LogVerbose("ActionPerformed - over time - removing old");
                                //finish out the old one
                                oldAction.endTime = oldAction.lastUpdateTime;
                                _completedInputActions.Enqueue(oldAction);
                            }

                            RGDebug.LogVerbose("ActionPerformed - over time - adding new action");
                            // add new one
                            activeAction = new KeyboardInputActionData
                            {
                                action = action.name,
                                binding = action.bindings[0].path,
                                startTime = context.startTime,
                                lastUpdateTime = context.time,
                                duration = context.time - context.startTime
                            };
                            _activeInputActions[action.name] = activeAction;
                        }
                        else
                        {
                            RGDebug.LogVerbose("ActionPerformed - still pushed- " + activeAction.action);
                            // still pushed.. update end time
                            activeAction.lastUpdateTime = context.time;
                            activeAction.duration = context.time - activeAction.startTime;
                        }
                    }
                    else
                    {
                        // NOT a Mac.. works correctly (ie: Windows 11, haven't tested Linux)
                        RGDebug.LogVerbose("ActionPerformed - still pushed- " + activeAction.action);
                        // still pushed.. update end time
                        activeAction.lastUpdateTime = context.time;
                        activeAction.duration = context.time - activeAction.startTime;
                    }
                }
            }
        }

        private void AddToResultList(ICollection<KeyboardInputActionData> list, KeyboardInputActionData action, double sentTime)
        {
            action.lastSentUpdateTime = sentTime;
            list.Add(action);
        }

        public List<KeyboardInputActionData> FlushInputDataBuffer(float upToTime = float.MaxValue)
        {
            // input events use unscaled time
            var currentTime = Time.unscaledTimeAsDouble;

            List<KeyboardInputActionData> result = new();
            while (_completedInputActions.TryPeek(out var completedAction))
            {
                if (completedAction.startTime < upToTime)
                {
                    _completedInputActions.TryDequeue(out _);
                    RGDebug.LogVerbose("Flush - adding completed - " + completedAction.action);
                    AddToResultList(result, completedAction, currentTime);
                }
            }

            var listOfActions = _activeInputActions.ToList();
            foreach (var activeAction in listOfActions)
            {
                if (activeAction.Value.startTime < upToTime)
                {
                    if (Application.platform == RuntimePlatform.OSXPlayer)
                    {
                        if (currentTime - activeAction.Value.lastUpdateTime > keyHeldThresholdSeconds)
                        {
                            // if already sent... it is done; don't send again - compared to ms decimal place
                            if ((int)((activeAction.Value.lastSentUpdateTime ?? 0) * 1000) >=
                                (int)(activeAction.Value.lastUpdateTime * 1000))
                            {
                                RGDebug.LogVerbose("Flush - Already sent update");
                                _activeInputActions.TryRemove(activeAction.Key, out _);
                            }
                            // not pushed for the threshold.. write it out, but also clean it up
                            else if (_activeInputActions.TryRemove(activeAction.Key, out var oldAction))
                            {
                                RGDebug.LogVerbose("Flush - remove old - " + oldAction.action);
                                oldAction.endTime = oldAction.lastUpdateTime;
                                AddToResultList(result, oldAction, currentTime);
                            }
                        }
                        else
                        {
                            //still pushed
                            RGDebug.LogVerbose("Flush - still pushed");
                            AddToResultList(result, activeAction.Value, currentTime);
                        }
                    }
                    else
                    {
                        // NOT a Mac.. works correctly (ie: Windows 11, haven't tested Linux)
                        //still pushed
                        RGDebug.LogVerbose("Flush - still pushed");
                        // windows doesn't keep sending events, so until cancel.. it's still pressed
                        activeAction.Value.lastUpdateTime = currentTime;
                        activeAction.Value.duration = currentTime - activeAction.Value.startTime;
                        AddToResultList(result, activeAction.Value, currentTime);
                    }
                }
            }

            return result;
        }
    }
}
