using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateActionTypes;
using RegressionGames.DebugUtils;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
#endif

// ReSharper disable InconsistentNaming
namespace RegressionGames.RGBotConfigs
{
 
    [RequireComponent(typeof(RGEntity))]
    [DisallowMultipleComponent]
    
    public class RGAction_KeyPress: RGAction
    {
        [Tooltip("Draw debug gizmos for current key inputs in editor runtime ?")]
        public bool renderDebugGizmos = false;

        public Vector3 debugGizmosOffset = new Vector3(0f,-1f,2f);
        
#if ENABLE_INPUT_SYSTEM
        // Allow this on any RGEntity that has an InputActionAsset
        public InputActionAsset InputAction;
#endif   
        
        private ConcurrentQueue<RG_KeyPress_Data> _keysToPress = new();

        private Dictionary<Key, InputControl> _inputActions = new();
        private InputControl _anyKey = null;
        
        private Dictionary<Key, double> keysDown = new();

        private RGGizmos RgGizmos;

        private void Start()
        {
#if ENABLE_INPUT_SYSTEM
            this.RgGizmos = new();
            if (InputAction == null)
            {
                throw new Exception("RGAction_KeyPress requires an InputActionAsset to be specified");
            }

            foreach (var inputActionActionMap in InputAction.actionMaps)
            {
                foreach (var inputAction in inputActionActionMap)
                {
                    foreach (var inputActionControl in inputAction.controls)
                    {
                        if (inputActionControl is KeyControl iac)
                        {
                            _inputActions.Add(iac.keyCode, inputActionControl);    
                        }
                        else if (inputActionControl is AnyKeyControl)
                        {
                            _anyKey = inputActionControl;
                        }
                    }
                }
            }
#endif
        }

        public void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // one button click per frame update
            if (_keysToPress.TryDequeue(out RG_KeyPress_Data keyToPress))
            {
                List<InputControl> actionsToTake = new();
                // check for any key 
                if (_anyKey != null)
                {
                    actionsToTake.Add(_anyKey);
                }

                var theKey = _inputActions.GetValueOrDefault(keyToPress.keyId, null);
                if (theKey != null)
                {
                    actionsToTake.Add(theKey);
                }

                foreach (var control in actionsToTake)
                {
                    PressKey(keyToPress.keyId, control, keyToPress.holdTime);
                }
            }
            
            // handle un-pressing any keys that have expired their time
            var keys = keysDown.Keys.ToList();
            foreach (var key in keys)
            {
                // this is < instead of <= so that keys last at least 1 frame
                if (keysDown[key] < Time.unscaledTime)
                {
                    // dictionary will have this value because we clicked it
                    UnPressKey(key, _inputActions.GetValueOrDefault(key));
                }
            }

            if (keysDown.Count == 0 && _anyKey != null)
            {
                // un-press the 'any' key
                UnPressKey(null, _anyKey);
            }
            
            int id = gameObject.transform.GetInstanceID();
            // render debug text
            if (renderDebugGizmos)
            {
                var debugText = "";
                var keysList = keysDown.ToList();
                keysList.Sort((a,b) => a.Key-b.Key);
                foreach (var (key,value) in keysList)
                {
                    debugText += $"{key.ToString()} : {(value-Time.unscaledTime):F2}\r\n";
                }

                
                if (string.IsNullOrEmpty(debugText))
                {
                    RgGizmos.DestroyText(id);
                }
                else
                {
                    RgGizmos.CreateText(id, debugText, debugGizmosOffset);
                }
            }
            else
            {
                RgGizmos.DestroyText(id);
            }
            
            // Update Input System
            InputSystem.Update();
#endif
        }

        private void OnDrawGizmos()
        {
            // force updating these now
            RgGizmos.OnDrawGizmos();
        }

        private void DrawDebugText()
        {
#if ENABLE_INPUT_SYSTEM
            // render debug text
            if (renderDebugGizmos)
            {
                var debugText = "";
                var keysList = keysDown.ToList();
                keysList.Sort((a,b) => (int)Math.Round(a.Value-b.Value, 0));
                foreach (var (key,value) in keysList)
                {
                    debugText += $"{key.ToString()} : {Math.Truncate((value-Time.unscaledTime) * 100) / 100}\r\n";
                }

                int id = gameObject.transform.GetInstanceID();
                if (string.IsNullOrEmpty(debugText))
                {
                    RgGizmos.DestroyText(id);
                }
                else
                {
                    RgGizmos.CreateText(id, debugText, debugGizmosOffset);
                }

                // force drawing these now
                RgGizmos.OnDrawGizmos();
            }
#endif
        }

        private void UnPressKey(Key? key, InputControl control)
        {
            // 1 == pressed state
            // 0 == un-pressed state
            void SetUpAndQueueEvent<TValue>(InputEventPtr eventPtr, TValue state) where TValue: struct
            {
                eventPtr.time = InputState.currentTime;
                control.WriteValueIntoEvent(state, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
            
            using (DeltaStateEvent.From(control, out var eventPtr))
            {
                SetUpAndQueueEvent(eventPtr, 0f);
                if (key != null)
                {
                    keysDown.Remove((Key)key);
                }
            }
        }

        private void PressKey(Key key, InputControl control, double holdTime = -1f)
        {
            // 1 == pressed state
            // 0 == un-pressed state
            void SetUpAndQueueEvent<TValue>(InputEventPtr eventPtr, TValue state) where TValue: struct
            {
                eventPtr.time = InputState.currentTime;
                control.WriteValueIntoEvent(state, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
            
            using (DeltaStateEvent.From(control, out var eventPtr))
            {
                var hasKeyDown = keysDown.ContainsKey(key);
                // set or update the un-press time tracker
                var timeToUnPress = Time.unscaledTime + (holdTime > 0f ? holdTime : 0f);
                keysDown[key] = timeToUnPress;
                if (!hasKeyDown)
                {
                    SetUpAndQueueEvent(eventPtr, 1f);
                }
            }
        }

        public override string GetActionName()
        {
            return "KeyPress";
        }

        public override void StartAction(Dictionary<string, object> input)
        {
            object keyId = input.GetValueOrDefault("keyId", null);
            object holdTime = input.GetValueOrDefault("holdTime", null);

            try
            {
                if (keyId != null)
                {
                    Key key;
                    if (keyId is Key id)
                    {
                        key = id;
                    }
                    else
                    {
                        if (!Enum.TryParse(keyId.ToString(), out key))
                        {
                            RGDebug.LogWarning(
                                $"WARNING: Ignoring RGAction_KeyPress with missing/invalid input for keyId: {keyId} or holdTime: {holdTime}");
                            return;
                        }
                    }
                    double? htDouble = null;
                    if (holdTime != null)
                    {
                        htDouble = Convert.ToDouble(holdTime);
                    }

                    _keysToPress.Enqueue(new RG_KeyPress_Data(key, htDouble));
                }
                else
                {
                    RGDebug.LogWarning(
                        $"WARNING: Ignoring RGAction_KeyPress with missing/invalid input for keyId: {keyId} or holdTime: {holdTime}");
                }
            }
            catch (Exception e)
            {
                RGDebug.LogWarning($"WARNING: Ignoring RGAction_KeyPress with missing/invalid input for keyId: {keyId} or holdTime: {holdTime}");
            }
        }
    }
    
    internal class RG_KeyPress_Data
    {
        public readonly Key keyId;
        public readonly double holdTime = -1f;

        public RG_KeyPress_Data(Key keyId, double? holdTime)
        {
            this.keyId = keyId;
            if (holdTime != null)
            {
                this.holdTime = (double)holdTime;
            }
        }
    }

    public class RGActionRequest_KeyPress : RGActionRequest
    {
        public RGActionRequest_KeyPress(Key keyId, double holdTime = -1f)
        {
            action = "KeyPress";
            Input = new() { { "keyId", keyId }, {"holdTime", holdTime} };
        }
    }
}
