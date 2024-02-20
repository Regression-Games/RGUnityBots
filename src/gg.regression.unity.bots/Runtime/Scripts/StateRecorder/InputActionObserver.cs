using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StateRecorder
{
    
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class InputActionData
    {
        public string action;
        public string binding;
        public double startTime;
        [NonSerialized]
        public double lastUpdateTime;
        [NonSerialized]
        public double? lastSentUpdateTime;
        public double? endTime;
        public double duration;
        public bool isPressed; // logically equivalent to (duration > 0 && endTime == null)
    }

    public class InputActionObserver : MonoBehaviour
    {
        public float keyHeldThresholdSeconds = 0.100f;
        
        private InputActionAsset _inputActionAsset;

        private readonly ConcurrentQueue<InputActionData> _inputDataQueue = new(); 
        
        private static InputActionObserver _this;

        private bool _recording = false;

        public static InputActionObserver GetInstance()
        {
            return _this;
        }

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

        public void StartRecording()
        {
            _activeInputActions.Clear();
            _recording = true;
        }

        public void StopRecording()
        {
            _recording = false;
        }

        private void Start()
        {
            
            //define action map
            var inputActionMap = new InputActionMap("RegressionGames");
            
            //row 1 (top row)
            CreateKeyboardAction(inputActionMap, "escape");
            CreateKeyboardAction(inputActionMap, "f1");
            CreateKeyboardAction(inputActionMap, "f2");
            CreateKeyboardAction(inputActionMap, "f3");
            CreateKeyboardAction(inputActionMap, "f4");
            CreateKeyboardAction(inputActionMap, "f5");
            CreateKeyboardAction(inputActionMap, "f6");
            CreateKeyboardAction(inputActionMap, "f7");
            CreateKeyboardAction(inputActionMap, "f8");
            CreateKeyboardAction(inputActionMap, "f9");
            CreateKeyboardAction(inputActionMap, "f10");
            CreateKeyboardAction(inputActionMap, "f11");
            CreateKeyboardAction(inputActionMap, "f12");
            CreateKeyboardAction(inputActionMap, "printScreen");
            CreateKeyboardAction(inputActionMap, "scrollLock");
            CreateKeyboardAction(inputActionMap, "pause");
            
            // row 2
            CreateKeyboardAction(inputActionMap, "backquote");
            CreateKeyboardAction(inputActionMap, "1");
            CreateKeyboardAction(inputActionMap, "2");
            CreateKeyboardAction(inputActionMap, "3");
            CreateKeyboardAction(inputActionMap, "4");
            CreateKeyboardAction(inputActionMap, "5");
            CreateKeyboardAction(inputActionMap, "6");
            CreateKeyboardAction(inputActionMap, "7");
            CreateKeyboardAction(inputActionMap, "8");
            CreateKeyboardAction(inputActionMap, "9");
            CreateKeyboardAction(inputActionMap, "0");
            CreateKeyboardAction(inputActionMap, "minus");
            CreateKeyboardAction(inputActionMap, "equals");
            CreateKeyboardAction(inputActionMap, "backspace");
            CreateKeyboardAction(inputActionMap, "insert");
            CreateKeyboardAction(inputActionMap, "home");
            CreateKeyboardAction(inputActionMap, "pageUp");
            CreateKeyboardAction(inputActionMap, "numLock");
            CreateKeyboardAction(inputActionMap, "numpadDivide");
            CreateKeyboardAction(inputActionMap, "numpadMultiply");
            CreateKeyboardAction(inputActionMap, "numpadMinus");
            
            //row 3
            CreateKeyboardAction(inputActionMap, "tab");
            CreateKeyboardAction(inputActionMap, "q");
            CreateKeyboardAction(inputActionMap, "w");
            CreateKeyboardAction(inputActionMap, "e");
            CreateKeyboardAction(inputActionMap, "r");
            CreateKeyboardAction(inputActionMap, "t");
            CreateKeyboardAction(inputActionMap, "y");
            CreateKeyboardAction(inputActionMap, "u");
            CreateKeyboardAction(inputActionMap, "i");
            CreateKeyboardAction(inputActionMap, "o");
            CreateKeyboardAction(inputActionMap, "p");
            CreateKeyboardAction(inputActionMap, "leftBracket");
            CreateKeyboardAction(inputActionMap, "rightBracket");
            CreateKeyboardAction(inputActionMap, "backSlash");
            CreateKeyboardAction(inputActionMap, "delete");
            CreateKeyboardAction(inputActionMap, "end");
            CreateKeyboardAction(inputActionMap, "pageDown");
            CreateKeyboardAction(inputActionMap, "numpad7");
            CreateKeyboardAction(inputActionMap, "numpad8");
            CreateKeyboardAction(inputActionMap, "numpad9");
            CreateKeyboardAction(inputActionMap, "numpadPlus"); ;
            
            //row 4
            CreateKeyboardAction(inputActionMap, "capsLock");
            CreateKeyboardAction(inputActionMap, "a");
            CreateKeyboardAction(inputActionMap, "s");
            CreateKeyboardAction(inputActionMap, "d");
            CreateKeyboardAction(inputActionMap, "f");
            CreateKeyboardAction(inputActionMap, "g");
            CreateKeyboardAction(inputActionMap, "h");
            CreateKeyboardAction(inputActionMap, "j");
            CreateKeyboardAction(inputActionMap, "k");
            CreateKeyboardAction(inputActionMap, "l");
            CreateKeyboardAction(inputActionMap, "semicolon");
            CreateKeyboardAction(inputActionMap, "quote");
            CreateKeyboardAction(inputActionMap, "enter");
            CreateKeyboardAction(inputActionMap, "numpad4");
            CreateKeyboardAction(inputActionMap, "numpad5");
            CreateKeyboardAction(inputActionMap, "numpad6");
            // big plus button already in row 3 
            
            //row 5
            CreateKeyboardAction(inputActionMap, "leftShift");
            CreateKeyboardAction(inputActionMap, "z");
            CreateKeyboardAction(inputActionMap, "x");
            CreateKeyboardAction(inputActionMap, "c");
            CreateKeyboardAction(inputActionMap, "v");
            CreateKeyboardAction(inputActionMap, "b");
            CreateKeyboardAction(inputActionMap, "n");
            CreateKeyboardAction(inputActionMap, "m");
            CreateKeyboardAction(inputActionMap, "comma");
            CreateKeyboardAction(inputActionMap, "period");
            CreateKeyboardAction(inputActionMap, "slash");
            CreateKeyboardAction(inputActionMap, "rightShift");
            CreateKeyboardAction(inputActionMap, "upArrow");
            CreateKeyboardAction(inputActionMap, "numpad1");
            CreateKeyboardAction(inputActionMap, "numpad2");
            CreateKeyboardAction(inputActionMap, "numpad3");
            CreateKeyboardAction(inputActionMap, "numpadEnter");

            // row 6 (bottom row)
            CreateKeyboardAction(inputActionMap, "leftCtrl");
            CreateKeyboardAction(inputActionMap, "leftMeta"); // windows Logo or mac-command key
            CreateKeyboardAction(inputActionMap, "leftAlt");
            CreateKeyboardAction(inputActionMap, "space");
            CreateKeyboardAction(inputActionMap, "rightAlt");
            CreateKeyboardAction(inputActionMap, "rightMeta"); // windows Fn or mac-command key
            CreateKeyboardAction(inputActionMap, "contextMenu");
            CreateKeyboardAction(inputActionMap, "rightCtrl");
            CreateKeyboardAction(inputActionMap, "leftArrow");
            CreateKeyboardAction(inputActionMap, "downArrow");
            CreateKeyboardAction(inputActionMap, "rightArrow");
            CreateKeyboardAction(inputActionMap, "numpad0");
            CreateKeyboardAction(inputActionMap, "numpadPeriod");
            // big enter button already in row 5
            
            // Keyboard OEM Special keys
            CreateKeyboardAction(inputActionMap, "OEM1");
            CreateKeyboardAction(inputActionMap, "OEM2");
            CreateKeyboardAction(inputActionMap, "OEM3");
            CreateKeyboardAction(inputActionMap, "OEM4");
            CreateKeyboardAction(inputActionMap, "OEM5");

            
            // setup control scheme
            var controlScheme = new InputControlScheme("RegressionKeyboardListener", new[] { new InputControlScheme.DeviceRequirement()
            {
                controlPath = Keyboard.current?.path,
                isAND = false,
                isOptional = true
            }});

            // setup the actionAsset
            _inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            _inputActionAsset.AddActionMap(inputActionMap);
            _inputActionAsset.AddControlScheme(controlScheme);
            _inputActionAsset.Enable();
            
        }

        private InputAction CreateKeyboardAction(InputActionMap inputActionMap, string keyName)
        {
            var inputAction = inputActionMap.AddAction("Keyboard/" + keyName, InputActionType.Value, "<Keyboard>/" + keyName);
            inputAction.performed += ActionPerformed;
            inputAction.canceled += ActionCanceled;
            return inputAction;
        }

        private ConcurrentDictionary<string, InputActionData> _activeInputActions = new();
        private ConcurrentQueue<InputActionData> _completedInputActions = new();
        
        void ActionCanceled(InputAction.CallbackContext context)
        {
            if (_recording)
            {
                Debug.Log("ActionCanceled - " + context.action.name);
                // record the end time

                    if (Application.platform == RuntimePlatform.OSXPlayer)
                    {
                        if (_activeInputActions.TryGetValue(context.action.name, out var actionData))
                        {
                            Debug.Log("ActionCanceled - updating");
                            // don't set action.isPressed here, we set it before sending back
                            actionData.lastUpdateTime = context.time;
                            actionData.duration = context.time - actionData.startTime;
                        }
                    }
                    else
                    {   // NOT a Mac.. works correctly (ie: Windows 11, haven't tested Linux)
                        if (_activeInputActions.TryRemove(context.action.name, out var actionData))
                        {
                            Debug.Log("ActionCanceled - end action");
                            actionData.lastUpdateTime = context.time;
                            actionData.duration = context.time - actionData.startTime;
                            actionData.endTime = context.time;
                            _completedInputActions.Enqueue(actionData);
                        }
                    }
                
            }
        }

        void ActionPerformed(InputAction.CallbackContext context)
        {
            if (_recording)
            {
                Debug.Log("ActionPerformed - " + context.action.name);
                var action = context.action;
                
                if (!_activeInputActions.TryGetValue(action.name, out var activeAction))
                {
                    Debug.Log("ActionPerformed - new action - " + action.name);
                    activeAction = new InputActionData()
                    {
                        action = action.name,
                        binding = action.bindings[0].path,
                        startTime = context.startTime,
                        lastUpdateTime = context.time,
                        duration = context.time - context.startTime,
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
                                Debug.Log("ActionPerformed - over time - removing old");
                                //finish out the old one
                                oldAction.endTime = oldAction.lastUpdateTime;
                                _completedInputActions.Enqueue(oldAction);
                            }

                            Debug.Log("ActionPerformed - over time - adding new action");
                            // add new one
                            activeAction = new InputActionData()
                            {
                                action = action.name,
                                binding = action.bindings[0].path,
                                startTime = context.startTime,
                                lastUpdateTime = context.time,
                                duration = context.time - context.startTime,
                            };
                            _activeInputActions[action.name] = activeAction;
                        }
                        else
                        {
                            Debug.Log("ActionPerformed - still pushed- " + activeAction.action);
                            // still pushed.. update end time
                            activeAction.lastUpdateTime = context.time;
                            activeAction.duration = context.time - activeAction.startTime;
                        }
                    } else {
                        // NOT a Mac.. works correctly (ie: Windows 11, haven't tested Linux)
                        Debug.Log("ActionPerformed - still pushed- " + activeAction.action);
                        // still pushed.. update end time
                        activeAction.lastUpdateTime = context.time;
                        activeAction.duration = context.time - activeAction.startTime;
                    }
                }
            }
        }

        private void AddToResultList(ICollection<InputActionData> list, InputActionData action, double sentTime)
        {
            action.isPressed = action.endTime == null && action.duration > 0;
            action.lastSentUpdateTime = sentTime;
            list.Add(action);
        }

        public List<InputActionData> FlushInputDataBuffer()
        {
            var currentTime = Time.unscaledTimeAsDouble;

            List<InputActionData> result = new();
            while (_completedInputActions.TryDequeue(out var completedAction))
            {
                Debug.Log("Flush - adding completed- " + completedAction.action);
                AddToResultList( result, completedAction, currentTime);
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
                            Debug.Log("Flush - Already sent update");
                            _activeInputActions.TryRemove(activeAction.Key, out _);
                        }
                        // not pushed for the threshold.. write it out, but also clean it up
                        else if (_activeInputActions.TryRemove(activeAction.Key, out var oldAction))
                        {
                            Debug.Log("Flush - remove old - " + oldAction.action);
                            oldAction.endTime = oldAction.lastUpdateTime;
                            AddToResultList(result, oldAction, currentTime);
                        }
                    }
                    else
                    {
                        //still pushed
                        Debug.Log("Flush - still pushed");
                        AddToResultList(result, activeAction.Value, currentTime);
                    }
                }
                else
                {
                    // NOT a Mac.. works correctly (ie: Windows 11, haven't tested Linux)
                    //still pushed
                    Debug.Log("Flush - still pushed");
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
