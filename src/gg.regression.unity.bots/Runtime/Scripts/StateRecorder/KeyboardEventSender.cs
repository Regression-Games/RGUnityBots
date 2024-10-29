using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.StateRecorder.Models;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder
{
    public static class KeyboardEventSender
    {
        private static bool _isShiftDown;

        // Stores the changed state of keys. When the Input System completes an update, this is cleared.
        // If multiple key state changes occur within the same frame, all of these are added to the keyboard event that is sent to the Input System.
        private static Dictionary<Key, KeyState> _keyStates = new ();

        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (!_isInitialized)
            {
                InputSystem.onAfterUpdate += OnAfterInputSystemUpdate;
                _isInitialized = true;
            }
        }

        /**
         * <summary>Cleans up the KeyboardEventSender by removing event handlers and resetting state</summary>
         */
        public static void TearDown(){
            if (_isInitialized)
            {
                InputSystem.onAfterUpdate -= OnAfterInputSystemUpdate;
                _isInitialized = false;
            }
            Reset();
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
        private static void QueueKeyboardUpdateEvent(int replaySegment)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
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
                    RGDebug.LogDebug($"({replaySegment}) [frame: {Time.frameCount}] - Queueing Keyboard Event to InputSystem");
                    InputSystem.QueueEvent(eventPtr);
                }
            }
        }

        /// <summary>
        /// Returns whether the key is already pressed or has a pending state change to be pressed
        /// </summary>
        private static bool IsKeyPressedOrPending(Key key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return keyboard[key].isPressed
                   || (_keyStates.TryGetValue(key, out KeyState state) && state == KeyState.Down);
        }

        private static void QueueTextEvent(int replaySegment, Key key)
        {
            var currentKeyboard = Keyboard.current;
            if (currentKeyboard != null)
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

                    var inputEvent = TextEvent.Create(currentKeyboard.deviceId, value, InputState.currentTime);
                    RGDebug.LogDebug($"({replaySegment}) Sending Text Event for char: '{value}'");
                    InputSystem.QueueEvent(ref inputEvent);
                }

                // If there are active UI input fields, simulate a KeyDown UI event for newly pressed keys
                // This simulation is done directly on the components, because there is no way to directly queue the event to Unity's event manager
                var inputFields = UnityEngine.Object.FindObjectsOfType<InputField>();
                var tmpInputFields = UnityEngine.Object.FindObjectsOfType<TMP_InputField>();
                if (inputFields.Length > 0 || tmpInputFields.Length > 0)
                {
                    var keyCode = RGLegacyInputUtils.InputSystemKeyToKeyCode(key);
                    Event evt = CreateUIKeyboardEvent(keyCode,
                        isShiftDown: _isShiftDown,
                        isCommandDown: IsKeyPressedOrPending(Key.LeftCommand) || IsKeyPressedOrPending(Key.RightCommand),
                        isAltDown: IsKeyPressedOrPending(Key.LeftAlt) || IsKeyPressedOrPending(Key.RightAlt),
                        isControlDown: IsKeyPressedOrPending(Key.LeftCtrl) || IsKeyPressedOrPending(Key.RightCtrl));
                    foreach (var inputField in inputFields)
                    {
                        if (inputField.isFocused)
                        {
                            SendKeyEventToInputField(evt, inputField);
                        }
                    }

                    foreach (var tmpInputField in tmpInputFields)
                    {
                        if (tmpInputField.isFocused)
                        {
                            SendKeyEventToInputField(evt, tmpInputField);
                        }
                    }
                }
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

            RGDebug.LogDebug($"({replaySegment}) Sending Multiple Key Event: [" +
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

            QueueKeyboardUpdateEvent(replaySegment);

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

            RGDebug.LogDebug($"({replaySegment}) [frame: {Time.frameCount}] - Sending Key Event: {key} - {upOrDown}");

            _keyStates[key] = upOrDown;

            QueueKeyboardUpdateEvent(replaySegment);

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

        /// <summary>
        /// Creates a UnityGUI keyboard event for simulating input events to UI components.
        /// </summary>
        private static Event CreateUIKeyboardEvent(KeyCode keyCode, bool isShiftDown, bool isCommandDown, bool isAltDown, bool isControlDown)
        {
            Event evt = new Event(0) { type = EventType.KeyDown };
            if (isShiftDown)
            {
                evt.modifiers |= EventModifiers.Shift;
            }
            if (isCommandDown)
            {
                evt.modifiers |= EventModifiers.Command;
            }
            if (isAltDown)
            {
                evt.modifiers |= EventModifiers.Alt;
            }
            if (isControlDown)
            {
                evt.modifiers |= EventModifiers.Control;
            }
            evt.keyCode = keyCode;

            var key = RGLegacyInputUtils.KeyCodeToInputSystemKey(keyCode);
            if (key != Key.None)
            {
                if (KeyboardInputActionObserver.KeyboardKeyToValueMap.TryGetValue(key, out var keyVal))
                {
                    if ((evt.modifiers & EventModifiers.Shift) != 0)
                    {
                        evt.character = keyVal.Item2;
                    }
                    else
                    {
                        evt.character = keyVal.Item1;
                    }
                }
            }

            return evt;
        }

        private static MethodInfo _inputFieldKeyPressedMethod;
        private static MethodInfo _tmpInputFieldKeyPressedMethod;

        /// <summary>
        /// Send the event to the given input field (either TMP_InputField or InputField)
        /// Note that this method should be used instead of the ProcessEvent() method that is already available,
        /// because ProcessEvent() does not correctly update the label or handle text submission
        /// </summary>
        private static void SendKeyEventToInputField(Event evt, InputField inputField)
        {
            if (_inputFieldKeyPressedMethod == null)
            {
                _inputFieldKeyPressedMethod =
                    typeof(InputField).GetMethod("KeyPressed", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            var editState = _inputFieldKeyPressedMethod.Invoke(inputField, new object[] { evt });
            if (editState.ToString() == "Finish")
            {
                if (!inputField.wasCanceled)
                {
                    if (inputField.onSubmit != null)
                        inputField.onSubmit.Invoke(inputField.text);
                }
                inputField.DeactivateInputField();
            }
            inputField.ForceLabelUpdate();
        }

        /// <summary>
        /// Sends key event to TextMeshPro input field (behaves the same as the one that targets the
        /// legacy InputField, just targets TextMeshPro instead)
        /// </summary>
        private static void SendKeyEventToInputField(Event evt, TMP_InputField inputField)
        {
            if (_tmpInputFieldKeyPressedMethod == null)
            {
                _tmpInputFieldKeyPressedMethod =
                    typeof(TMP_InputField).GetMethod("KeyPressed", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            var editState = _tmpInputFieldKeyPressedMethod.Invoke(inputField, new object[] { evt });
            if (editState.ToString() == "Finish")
            {
                if (!inputField.wasCanceled)
                {
                    if (inputField.onSubmit != null)
                        inputField.onSubmit.Invoke(inputField.text);
                }
                inputField.DeactivateInputField();
            }
            inputField.ForceLabelUpdate();
        }
    }
}
