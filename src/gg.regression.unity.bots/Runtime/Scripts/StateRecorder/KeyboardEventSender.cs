using System.Linq;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.StateRecorder.Models;
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

        public static void SendKeyEvent(int replaySegment, KeyboardInputActionData keyboardData, KeyState upOrDown)
        {
            var key = keyboardData.Key;
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
                    RGDebug.LogInfo($"({replaySegment}) Sending Key Event: [{keyboardData.Replay_StartTime}] [{keyboardData.Replay_EndTime}] - {key} - {upOrDown}");

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
                        InputSystem.QueueEvent(ref inputEvent);
                    }
                }
            }
        }

#if ENABLE_LEGACY_INPUT_MANAGER
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
