using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.StateRecorder.Models;
using Unity.Collections;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace RegressionGames.StateRecorder
{
    public static class KeyboardEventSender
    {
        private static bool _isShiftDown;

        public static void Reset()
        {
            _isShiftDown = false;
        }

        /**
         * Allows updating multiple keys in the same event.  This will break up into multiple events if the same key is passed more than once
         * or if the shift key is pressed
         */
        public static void SendKeysInOneEvent(int replaySegment, List<(Key, KeyState)> keyStates)
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            SendKeysInOneEventLegacy(keyStates);
            #endif
            
            var time = InputState.currentTime;
            var keyboard = Keyboard.current;

            var keys = new HashSet<Key>();
            NativeArray<byte>? deltaStateEventArray = null;
            InputEventPtr? deltaStateEvent = null;
            try
            {
                List<(Key, KeyState)> logList = new();

                foreach (var valueTuple in keyStates)
                {
                    var key = valueTuple.Item1;
                    var upOrDown = valueTuple.Item2;

                    if (deltaStateEvent.HasValue)
                    {
                        if (key == Key.LeftShift || key == Key.RightShift)
                        {
                            // start a new event
                            _isShiftDown = upOrDown == KeyState.Down;
                            RGDebug.LogInfo($"({replaySegment}) Sending Multiple Key Event: [" + string.Join(", ", logList.Select(a => a.Item1 + ":" + a.Item2).ToArray()) + "]");
                            logList.Clear();
                            InputSystem.QueueEvent(deltaStateEvent.Value);
                            deltaStateEvent = null;
                            deltaStateEventArray.Value.Dispose();
                            deltaStateEventArray = null;
                        }
                        else
                        {
                            if (keys.Contains(key))
                            {
                                RGDebug.LogInfo($"({replaySegment}) Sending Multiple Key Event: [" + string.Join(", ", logList.Select(a => a.Item1 + ":" + a.Item2).ToArray()) + "]");
                                logList.Clear();
                                InputSystem.QueueEvent(deltaStateEvent.Value);
                                deltaStateEvent = null;
                                deltaStateEventArray.Value.Dispose();
                                deltaStateEventArray = null;
                                keys.Clear();
                            }
                        }
                    }
                    if (!deltaStateEvent.HasValue)
                    {
                        deltaStateEventArray = DeltaStateEvent.From(keyboard, out var newEvent);
                        deltaStateEvent = newEvent;
                    }

                    logList.Add(valueTuple);
                    keys.Add(key);
                    var inputControl = keyboard.allControls
                        .FirstOrDefault(a => a is KeyControl kc && kc.keyCode == key) ?? keyboard.anyKey;
                    if (inputControl != null)
                    {
                        inputControl.WriteValueIntoEvent(upOrDown == KeyState.Down ? 1f : 0f, deltaStateEvent.Value);

                        if (upOrDown == KeyState.Down)
                        {
                            // send a text event so that 'onChange' text events fire
                            // convert key to text
                            if (KeyboardInputActionObserver.KeyboardKeyToValueMap.TryGetValue(((KeyControl)inputControl).keyCode, out var possibleValues))
                            {
                                var value = _isShiftDown ? possibleValues.Item2 : possibleValues.Item1;
                                if (value == 0x00)
                                {
                                    RGDebug.LogError($"Found null value for keyboard input {key}");
                                    return;
                                }

                                var inputEvent = TextEvent.Create(Keyboard.current.deviceId, value, time);
                                RGDebug.LogInfo($"({replaySegment}) Sending Text Event for char: '{value}'");
                                InputSystem.QueueEvent(ref inputEvent);
                            }
                        }
                    }

                }

                if (deltaStateEvent.HasValue)
                {
                    RGDebug.LogInfo($"({replaySegment}) Sending Multiple Key Event: [" + string.Join(", ", logList.Select(a => a.Item1 + ":" + a.Item2).ToArray()) + "]");
                    InputSystem.QueueEvent(deltaStateEvent.Value);
                    logList.Clear();
                }
            }
            finally
            {
                if (deltaStateEventArray.HasValue)
                {
                    deltaStateEventArray.Value.Dispose();
                }
            }
        }

        public static void SendKeyEvent(int replaySegment, Key key, KeyState upOrDown)
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            SendKeyEventLegacy(key, upOrDown);
            #endif

            var keyboard = Keyboard.current;

            if (key == Key.LeftShift || key == Key.RightShift)
            {
                _isShiftDown = upOrDown == KeyState.Down;
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
                    RGDebug.LogInfo($"({replaySegment}) Sending Key Event: {key} - {upOrDown}");

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
                        var value = _isShiftDown ? possibleValues.Item2 : possibleValues.Item1;
                        if (value == 0x00)
                        {
                            RGDebug.LogError($"Found null value for keyboard input {key}");
                            return;
                        }

                        var inputEvent = TextEvent.Create(Keyboard.current.deviceId, value, time);
                        RGDebug.LogInfo($"({replaySegment}) Sending Text Event for char: '{value}'");
                        InputSystem.QueueEvent(ref inputEvent);
                    }
                }
            }
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private static void SendKeysInOneEventLegacy(List<(Key, KeyState)> keyStates)
        {
            if (RGLegacyInputWrapper.IsPassthrough)
            {
                // simulation not started
                return;
            }

            foreach ((Key key, KeyState upOrDown) in keyStates)
            {
                SendKeyEventLegacy(key, upOrDown);
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
