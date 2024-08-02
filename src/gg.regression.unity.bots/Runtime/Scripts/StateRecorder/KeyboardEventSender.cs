using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace RegressionGames.StateRecorder
{
    public static class KeyboardEventSender
    {
        private static bool _isShiftDown;
        
        // Stores the changed state of keys. When the Input System completes an update, this is cleared.
        // If multiple key state changes occur within the same frame, all of these are added to the keyboard event that is sent to the Input System.
        private static Dictionary<Key, KeyState> _keyStates = new Dictionary<Key, KeyState>();

        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (!_isInitialized)
            {
                InputSystem.onAfterUpdate += OnAfterInputSystemUpdate;
                _isInitialized = true;
            }
        }

        private static void OnAfterInputSystemUpdate()
        {
            _keyStates.Clear();
        }

        public static void Reset()
        {
            _isShiftDown = false;
            _keyStates.Clear();
        }

        /// <summary>
        /// Queues a StateEvent onto the InputSystem event queue containing the desired state changes
        /// in the _keyStates field.
        /// </summary>
        private static void QueueKeyboardUpdateEvent()
        {
            var keyboard = Keyboard.current;
            using (StateEvent.From(keyboard, out var eventPtr))
            {
                var time = InputState.currentTime;
                eventPtr.time = time;

                // Include all key states that have been changed since the last InputSystem update cycle
                foreach (var entry in _keyStates)
                {
                    var inputControl = keyboard.allControls.FirstOrDefault(a => a is KeyControl kc && kc.keyCode == entry.Key);
                    if (inputControl != null)
                    {
                        inputControl.WriteValueIntoEvent(entry.Value == KeyState.Down ? 1f : 0f, eventPtr);
                    }
                }
                
                InputSystem.QueueEvent(eventPtr);
            }
        }

        private static void QueueTextEvent(int replaySegment, Key key)
        {
            // send a text event so that 'onChange' text events fire
            // convert key to text
            if (KeyboardInputActionObserver.KeyboardKeyToValueMap.TryGetValue(key, out var possibleValues))
            {
                var value = _isShiftDown ? possibleValues.Item2 : possibleValues.Item1;
                if (value == 0x00)
                {
                    RGDebug.LogError($"Found null value for keyboard input {key}");
                    return;
                }

                var inputEvent = TextEvent.Create(Keyboard.current.deviceId, value, InputState.currentTime);
                RGDebug.LogInfo($"({replaySegment}) Sending Text Event for char: '{value}'");
                InputSystem.QueueEvent(ref inputEvent);
            }
        }

        /**
         * Allows updating multiple keys in the same event. 
         */
        public static void SendKeysInOneEvent(int replaySegment, IDictionary<Key, KeyState> keyStates)
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            SendKeysInOneEventLegacy(keyStates);
            #endif
            
            RGDebug.LogInfo($"({replaySegment}) Sending Multiple Key Event: [" + 
                            string.Join(", ", keyStates.Select(a => a.Key + ":" + a.Value).ToArray()) + "]");
            
            foreach (var (key, upOrDown) in keyStates)
            {
                if (_keyStates.ContainsKey(key))
                {
                    RGDebug.LogWarning($"KeyboardEventSender - Multiple key events have been sent within one frame for {key}. Only the last state ({upOrDown}) will be kept.");
                }
            
                _keyStates[key] = upOrDown;
                
                if (key == Key.LeftShift || key == Key.RightShift)
                {
                    _isShiftDown = upOrDown == KeyState.Down;
                }
            }
            
            QueueKeyboardUpdateEvent();

            foreach (var entry in keyStates)
            {
                var key = entry.Key;
                var upOrDown = entry.Value;
                if (upOrDown == KeyState.Down)
                {
                    QueueTextEvent(replaySegment, key);
                }
            }
        }
        
        public static void SendKeyEvent(int replaySegment, Key key, KeyState upOrDown)
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            SendKeyEventLegacy(key, upOrDown);
            #endif

            if (key == Key.LeftShift || key == Key.RightShift)
            {
                _isShiftDown = upOrDown == KeyState.Down;
            }

            if (_keyStates.ContainsKey(key))
            {
                RGDebug.LogWarning($"KeyboardEventSender - Multiple key events have been sent within one frame for {key}. Only the last state ({upOrDown}) will be kept.");
            }
            
            RGDebug.LogInfo($"({replaySegment}) Sending Key Event: {key} - {upOrDown}");
            
            _keyStates[key] = upOrDown;
            
            QueueKeyboardUpdateEvent();

            if (upOrDown == KeyState.Down)
            {
                QueueTextEvent(replaySegment, key);
            }
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private static void SendKeysInOneEventLegacy(IDictionary<Key, KeyState> keyStates)
        {
            if (RGLegacyInputWrapper.IsPassthrough)
            {
                // simulation not started
                return;
            }

            foreach (var entry in keyStates)
            {
                SendKeyEventLegacy(entry.Key, entry.Value);
            }
        }
        
        private static void SendKeyEventLegacy(Key key, KeyState upOrDown)
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
    }
}
