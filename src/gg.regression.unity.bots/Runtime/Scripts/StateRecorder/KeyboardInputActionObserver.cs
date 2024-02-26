using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using RegressionGames;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.StateRecorder
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class KeyboardInputActionData : InputActionData
    {
        public string action;
        public string binding;
        public double duration;
        public double? endTime;
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

        public static readonly IReadOnlyCollection<string> AllKeyboardKeys = new List<string>
        {
            //row 1 (top row)
            "escape",
            "f1",
            "f2",
            "f3",
            "f4",
            "f5",
            "f6",
            "f7",
            "f8",
            "f9",
            "f10",
            "f11",
            "f12",
            "printScreen",
            "scrollLock",
            "pause",

            // row 2
            "backquote",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "0",
            "minus",
            "equals",
            "backspace",
            "insert",
            "home",
            "pageUp",
            "numLock",
            "numpadDivide",
            "numpadMultiply",
            "numpadMinus",

            //row 3
            "tab",
            "q",
            "w",
            "e",
            "r",
            "t",
            "y",
            "u",
            "i",
            "o",
            "p",
            "leftBracket",
            "rightBracket",
            "backSlash",
            "delete",
            "end",
            "pageDown",
            "numpad7",
            "numpad8",
            "numpad9",
            "numpadPlus",

            //row 4
            "capsLock",
            "a",
            "s",
            "d",
            "f",
            "g",
            "h",
            "j",
            "k",
            "l",
            "semicolon",
            "quote",
            "enter",
            "numpad4",
            "numpad5",
            "numpad6",
            // big plus button already in row 3

            //row 5
            "leftShift",
            "z",
            "x",
            "c",
            "v",
            "b",
            "n",
            "m",
            "comma",
            "period",
            "slash",
            "rightShift",
            "upArrow",
            "numpad1",
            "numpad2",
            "numpad3",
            "numpadEnter",

            // row 6 (bottom row)
            "leftCtrl",
            "leftMeta", // windows Logo or
            "leftAlt",
            "space",
            "rightAlt",
            "rightMeta", // windows Fn or m
            "contextMenu",
            "rightCtrl",
            "leftArrow",
            "downArrow",
            "rightArrow",
            "numpad0",
            "numpadPeriod",
            // big enter button already in row 5

            // Keyboard OEM Special keys
            "OEM1",
            "OEM2",
            "OEM3",
            "OEM4",
            "OEM5",
        }.AsReadOnly();

        private void Start()
        {
            //define action map
            var inputActionMap = new InputActionMap("RegressionGames");

            foreach (var keyboardKey in AllKeyboardKeys)
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

        private void AddToResultList(ICollection<InputActionData> list, KeyboardInputActionData action, double sentTime)
        {
            action.lastSentUpdateTime = sentTime;
            list.Add(action);
        }

        public List<InputActionData> FlushInputDataBuffer()
        {
            var currentTime = Time.unscaledTimeAsDouble;

            List<InputActionData> result = new();
            while (_completedInputActions.TryDequeue(out var completedAction))
            {
                RGDebug.LogVerbose("Flush - adding completed- " + completedAction.action);
                AddToResultList(result, completedAction, currentTime);
            }

            var listOfActions = _activeInputActions.ToList();
            foreach (var activeAction in listOfActions)
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

            return result;
        }
    }
}
