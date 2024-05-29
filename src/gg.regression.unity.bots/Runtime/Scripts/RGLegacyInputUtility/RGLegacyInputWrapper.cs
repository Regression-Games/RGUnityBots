#if ENABLE_LEGACY_INPUT_MANAGER 
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RegressionGames.RGLegacyInputUtility
{
    class RGLegacyAxisInputState
    {
        public float rawValue;
        public float smoothedValue;

        public RGLegacyAxisInputState()
        {
            rawValue = 0.0f;
            smoothedValue = 0.0f;
        }
    }
    
    /**
     * Class that provides APIs for simulating device inputs, and also
     * provides the necessary wrappers for the UnityEngine.Input APIs that
     * are automatically added by RGLegacyInputInstrumentation.
     *
     * When adding new Input wrappers to this class, you should also update
     * RGBaseInput with the wrappers that have been added.
     */
    public static class RGLegacyInputWrapper
    {
        private static MonoBehaviour _simulationContext;
        private static ISet<KeyCode> _keysHeld;
        private static ISet<KeyCode> _newKeysDown;
        private static ISet<KeyCode> _newKeysUp;
        private static bool _anyKeyDown;
        private static Vector3 _mousePosition;
        private static Vector3 _mousePosDelta;
        private static Vector2 _mouseScrollDelta;
        private static Dictionary<InputManagerEntry, RGLegacyAxisInputState> _axisStates;
        private static Dictionary<KeyCode, Coroutine> _removeNewCoroutines;
        private static Coroutine _clearAnyKeyDownCoro;
        private static Coroutine _clearMousePosDeltaCoro;
        private static Coroutine _clearMouseScrollDeltaCoro;
        private static RGLegacyInputManagerSettings _inputManagerSettings;
        private static Coroutine _inputSimLoopCoro;
        
        /**
         * If this flag is true (this is the initial state), then all inputs are
         * forwarded to the regular UnityEngine.Input APIs.
         * 
         * If this flag is false (after calling StartSimulation), then the test driver
         * has control of the inputs via the Simulate... methods and the game will
         * no longer read inputs from the user's device.
         */
        public static bool IsPassthrough
        {
            get => _simulationContext == null;
        }

        /**
         * Called by the test driver to take control of the user input.
         * Context parameter is the driving MonoBehaviour to use as context.
         */
        public static void StartSimulation(MonoBehaviour context)
        {
            if (_simulationContext != null)
            {
                throw new InvalidOperationException("An active simulation is already in progress");
            }

            _simulationContext = context;
            _keysHeld = new HashSet<KeyCode>();
            _newKeysDown = new HashSet<KeyCode>();
            _newKeysUp = new HashSet<KeyCode>();
            _anyKeyDown = false;
            _mousePosition = Vector3.zero;
            _mousePosDelta = Vector3.zero;
            _mouseScrollDelta = Vector2.zero;
            _axisStates = new Dictionary<InputManagerEntry, RGLegacyAxisInputState>();
            _removeNewCoroutines = new Dictionary<KeyCode, Coroutine>();
            _inputManagerSettings = new RGLegacyInputManagerSettings();
            _inputSimLoopCoro = _simulationContext.StartCoroutine(InputSimLoop());
            
            // initialize axis states
            foreach (var entry in _inputManagerSettings.Entries)
            {
                _axisStates[entry] = new RGLegacyAxisInputState();
            }
        }
        
        /**
         * Called by the test driver to release control of the user input.
         */
        public static void StopSimulation()
        {
            if (_simulationContext != null)
            {
                _simulationContext.StopCoroutine(_inputSimLoopCoro);
                foreach (Coroutine coro in _removeNewCoroutines.Values)
                {
                    _simulationContext.StopCoroutine(coro);
                }
                if (_clearAnyKeyDownCoro != null)
                {
                    _simulationContext.StopCoroutine(_clearAnyKeyDownCoro);
                    _clearAnyKeyDownCoro = null;
                }
                if (_clearMousePosDeltaCoro != null)
                {
                    _simulationContext.StopCoroutine(_clearMousePosDeltaCoro);
                    _clearMousePosDeltaCoro = null;
                }
                if (_clearMouseScrollDeltaCoro != null)
                {
                    _simulationContext.StopCoroutine(_clearMouseScrollDeltaCoro);
                    _clearMouseScrollDeltaCoro = null;
                }
                _inputManagerSettings = null;
                _newKeysDown = null;
                _newKeysUp = null;
                _axisStates = null;
                _keysHeld = null;
                _removeNewCoroutines = null;
                _simulationContext = null;
            }
        }

        private static IEnumerator InputSimLoop()
        {
            for (;;)
            {
                foreach (var p in _axisStates)
                {
                    InputManagerEntry entry = p.Key;
                    RGLegacyAxisInputState state = p.Value;
                    state.rawValue = 0.0f;
                    if (entry.type == InputManagerEntryType.KEY_OR_MOUSE_BUTTON)
                    {
                        bool anyHeld = false;
                        if ((entry.negativeButtonKeyCode.HasValue && _keysHeld.Contains(entry.negativeButtonKeyCode.Value))
                            || (entry.altNegativeButtonKeyCode.HasValue && _keysHeld.Contains(entry.altNegativeButtonKeyCode.Value)))
                        {
                            state.rawValue += -1.0f;
                            anyHeld = true;
                        }
                        if ((entry.positiveButtonKeyCode.HasValue && _keysHeld.Contains(entry.positiveButtonKeyCode.Value))
                            || (entry.altPositiveButtonKeyCode.HasValue && _keysHeld.Contains(entry.altPositiveButtonKeyCode.Value)))
                        {
                            state.rawValue += 1.0f;
                            anyHeld = true;
                        }
                        if (entry.invert)
                        {
                            state.rawValue *= -1.0f;
                        }
                        float startVal;
                        if (state.smoothedValue >= 0.0f)
                        {
                            // if snap is enabled, the starting value should be 0 if raw value goes in different direction
                            startVal = entry.snap ? (state.rawValue < 0.0f ? 0.0f : state.smoothedValue) : state.smoothedValue;
                        }
                        else
                        {
                            startVal = entry.snap ? (state.rawValue > 0.0f ? 0.0f : state.smoothedValue) : state.smoothedValue;
                        }
                        float rate = (anyHeld ? entry.sensitivity : entry.gravity) * Time.deltaTime;
                        state.smoothedValue = Mathf.MoveTowards(startVal, state.rawValue, rate);
                    } 
                    else if (entry.type == InputManagerEntryType.MOUSE_MOVEMENT)
                    {
                        if (entry.axis == 0) // X Axis
                        {
                            state.rawValue = _mousePosDelta.x * entry.sensitivity;
                        } 
                        else if (entry.axis == 1) // Y Axis
                        {
                            state.rawValue = _mousePosDelta.y * entry.sensitivity;
                        }
                        else if (entry.axis == 2) // Scroll Wheel
                        {
                            state.rawValue = _mouseScrollDelta.y * entry.sensitivity;
                        }
                        if (entry.invert)
                        {
                            state.rawValue *= -1.0f;
                        }
                        state.smoothedValue = state.rawValue; // smoothing is not applied on mouse movement
                    } 
                    else if (entry.type == InputManagerEntryType.JOYSTICK_AXIS)
                    {
                        // joysticks unsupported currently
                        // eventually this will read the joystick value, incorporating entry.dead for dead zone
                    } 
                    else
                    {
                        RGDebug.LogWarning($"Unexpected input manager entry type: {entry.type}");
                    }
                }
                yield return null;
            }
        }
        
        /**
         * Coroutine that clears "new" flag on a key (either down or up)
         * on the next frame.
         */
        private static IEnumerator RemoveNewKey(KeyCode keyCode)
        {
            yield return null;
            _newKeysDown.Remove(keyCode);
            _newKeysUp.Remove(keyCode);
        }

        private static void ScheduleRemoveNewKey(KeyCode keyCode)
        {
            _removeNewCoroutines.Add(keyCode, _simulationContext.StartCoroutine(RemoveNewKey(keyCode)));
        }

        /**
         * Coroutine for clearing the "any key down" flag on the next frame.
         */
        private static IEnumerator ClearAnyKeyDown()
        {
            yield return null;
            _anyKeyDown = false;
        }
        
        private static void ClearKeyState(KeyCode keyCode)
        {
            if (_removeNewCoroutines.Remove(keyCode, out Coroutine coro))
            {
                _simulationContext.StopCoroutine(coro);
            }
            _keysHeld.Remove(keyCode);
            _newKeysDown.Remove(keyCode);
            _newKeysUp.Remove(keyCode);
        }

        /**
         * Called by the test driver to simulate a key press.
         */
        public static void SimulateKeyPress(KeyCode keyCode)
        {
            bool wasHeld = _keysHeld.Contains(keyCode);
            bool anyHeld = _keysHeld.Count > 0;
            ClearKeyState(keyCode);
            _keysHeld.Add(keyCode);
            if (!wasHeld)
            {
                _newKeysDown.Add(keyCode);
                ScheduleRemoveNewKey(keyCode);
            }
            if (!anyHeld)
            {
                _anyKeyDown = true;
                if (_clearAnyKeyDownCoro != null)
                {
                    _simulationContext.StopCoroutine(_clearAnyKeyDownCoro);
                }
                _clearAnyKeyDownCoro = _simulationContext.StartCoroutine(ClearAnyKeyDown());
            }
        }

        /**
         * Called by the test driver to simulate a key release.
         */
        public static void SimulateKeyRelease(KeyCode keyCode)
        {
            bool wasHeld = _keysHeld.Contains(keyCode);
            ClearKeyState(keyCode);
            if (wasHeld)
            {
                _newKeysUp.Add(keyCode);
                ScheduleRemoveNewKey(keyCode);
            }
        }

        // Coroutine for clearing the mouse movement delta on the next frame
        private static IEnumerator ClearMouseMovementDelta()
        {
            yield return null;
            _mousePosDelta = Vector3.zero;
            _clearMousePosDeltaCoro = null;
        }

        /**
         * Called by the test driver to simulate a mouse movement to a new position.
         */
        public static void SimulateMouseMovement(Vector3 newMousePosition)
        {
            _mousePosDelta = newMousePosition - _mousePosition;
            _mousePosition = newMousePosition;
            if (_clearMousePosDeltaCoro != null)
            {
                _simulationContext.StopCoroutine(_clearMousePosDeltaCoro);
            }
            _clearMousePosDeltaCoro = _simulationContext.StartCoroutine(ClearMouseMovementDelta());
        }

        // Coroutine for clearing the mouse scroll delta on the next frame
        private static IEnumerator ClearMouseScrollDelta()
        {
            yield return null;
            _mouseScrollDelta = Vector2.zero;
            _clearMouseScrollDeltaCoro = null;
        }

        public static void SimulateMouseScrollWheel(Vector2 newMouseScrollDelta)
        {
            _mouseScrollDelta = newMouseScrollDelta;
            if (_clearMouseScrollDeltaCoro != null)
            {
                _simulationContext.StopCoroutine(_clearMouseScrollDeltaCoro);
            }
            _clearMouseScrollDeltaCoro = _simulationContext.StartCoroutine(ClearMouseScrollDelta());
        }
        
        private static Regex _joystickButtonPattern = new Regex(@"joystick (\d+) button (\d+)", RegexOptions.Compiled);
        
        // Based on https://docs.unity3d.com/Manual/class-InputManager.html
        public static KeyCode KeyNameToCode(string buttonName)
        {
            if (buttonName.StartsWith("joystick button"))
            {
                return (KeyCode)Enum.Parse(typeof(KeyCode), "JoystickButton" + int.Parse(buttonName.Replace("joystick button", "").Trim()));
            }
            else if (buttonName.StartsWith("mouse "))
            {
                return KeyCode.Mouse0 + int.Parse(buttonName.Substring(6).Trim());
            }
            else
            {
                Match joystickMatch = _joystickButtonPattern.Match(buttonName);
                if (joystickMatch.Success)
                {
                    return (KeyCode)Enum.Parse(typeof(KeyCode),
                        "Joystick" + joystickMatch.Groups[1].Value + "Button" + joystickMatch.Groups[2].Value);
                }
                switch (buttonName)
                {
                    case "right shift":
                        return KeyCode.RightShift;
                    case "left shift":
                        return KeyCode.LeftShift;
                    case "right ctrl":
                        return KeyCode.RightControl;
                    case "left ctrl":
                        return KeyCode.LeftControl;
                    case "right alt":
                        return KeyCode.RightAlt;
                    case "left alt":
                        return KeyCode.LeftAlt;
                    case "right cmd":
                        return KeyCode.RightCommand;
                    case "left cmd":
                        return KeyCode.LeftCommand;
                    case "enter":
                        return KeyCode.Return;
                    default:
                        return Event.KeyboardEvent(buttonName).keyCode;
                }
            }
        }
        
        ////////////////////////////////////////////////////////////////////////////////////////////
        // The following code defines the wrapper methods corresponding to the UnityEngine.Input API
        // The RGLegacyInputInstrumentation class will automatically redirect the legacy input API invocations to these.
        ////////////////////////////////////////////////////////////////////////////////////////////

        public static bool anyKey
        {
            get
            {
                if (IsPassthrough)
                {
                    return UnityEngine.Input.anyKey;
                }
                else
                {
                    return _keysHeld.Count > 0;
                }
            }
        }

        public static bool anyKeyDown
        {
            get
            {
                if (IsPassthrough)
                {
                    return UnityEngine.Input.anyKeyDown;
                }
                else
                {
                    return _anyKeyDown;
                }
            }
        }

        public static bool mousePresent
        {
            get
            {
                if (IsPassthrough)
                {
                    return UnityEngine.Input.mousePresent;
                }
                else
                {
                    return true;
                }
            }
        }

        public static Vector3 mousePosition
        {
            get
            {
                if (IsPassthrough)
                {
                    return UnityEngine.Input.mousePosition;
                }
                else
                {
                    return _mousePosition;
                }
            }
        }

        public static Vector2 mouseScrollDelta
        {
            get
            {
                if (IsPassthrough)
                {
                    return UnityEngine.Input.mouseScrollDelta;
                }
                else
                {
                    return _mouseScrollDelta;
                }
            }
        }
        
        public static bool GetKey(string name)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetKey(name);
            }
            else
            {
                return _keysHeld.Contains(KeyNameToCode(name));
            }
        }

        public static bool GetKey(KeyCode key)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetKey(key);
            }
            else
            {
                return _keysHeld.Contains(key);
            }
        }

        public static bool GetKeyDown(string name)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetKeyDown(name);
            }
            else
            {
                return _newKeysDown.Contains(KeyNameToCode(name));
            }
        }

        public static bool GetKeyDown(KeyCode key)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetKeyDown(key);
            }
            else
            {
                return _newKeysDown.Contains(key);
            }
        }

        public static bool GetKeyUp(string name)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetKeyUp(name);
            }
            else
            {
                return _newKeysUp.Contains(KeyNameToCode(name));
            }
        }

        public static bool GetKeyUp(KeyCode key)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetKeyUp(key);
            }
            else
            {
                return _newKeysUp.Contains(key);
            }
        }
        
        private static KeyCode MouseButtonToKeyCode(int button)
        {
        	if (button < 0 || button > 6)
        	{
        		throw new ArgumentOutOfRangeException("mouse button " + button + " out of range");
        	}
        	return KeyCode.Mouse0 + button;
        }

        public static bool GetMouseButton(int button)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetMouseButton(button);
            }
            else
            {
                return GetKey(MouseButtonToKeyCode(button));
            }
        }

        public static bool GetMouseButtonDown(int button)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetMouseButtonDown(button);
            }
            else
            {
                return GetKeyDown(MouseButtonToKeyCode(button));
            }
        }

        public static bool GetMouseButtonUp(int button)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetMouseButtonUp(button);
            }
            else
            {
                return GetKeyUp(MouseButtonToKeyCode(button));
            }
        }

        public static float GetAxisRaw(string name)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetAxisRaw(name);
            }
            else
            {
                foreach (var entry in _inputManagerSettings.GetEntriesByName(name))
                {
                    var state = _axisStates[entry];
                    if (Mathf.Abs(state.rawValue) >= float.Epsilon)
                    {
                        return state.rawValue;
                    }
                }
                return 0.0f;
            }
        }
        
        public static float GetAxis(string name)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetAxis(name);
            }
            else
            {
                foreach (var entry in _inputManagerSettings.GetEntriesByName(name))
                {
                    var state = _axisStates[entry];
                    if (Mathf.Abs(state.smoothedValue) >= float.Epsilon)
                    {
                        return state.smoothedValue;
                    }
                }
                return 0.0f;
            }
        }

        public static bool GetButton(string buttonName)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetButton(buttonName);
            }
            else
            {
                foreach (var entry in _inputManagerSettings.GetEntriesByName(buttonName))
                {
                    if ((entry.positiveButtonKeyCode.HasValue && _keysHeld.Contains(entry.positiveButtonKeyCode.Value))
                    || (entry.negativeButtonKeyCode.HasValue && _keysHeld.Contains(entry.negativeButtonKeyCode.Value))
                    || (entry.altPositiveButtonKeyCode.HasValue && _keysHeld.Contains(entry.altPositiveButtonKeyCode.Value))
                    || (entry.altNegativeButtonKeyCode.HasValue && _keysHeld.Contains(entry.altNegativeButtonKeyCode.Value)))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public static bool GetButtonDown(string buttonName)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetButtonDown(buttonName);
            }
            else
            {
                foreach (var entry in _inputManagerSettings.GetEntriesByName(buttonName))
                {
                    if ((entry.positiveButtonKeyCode.HasValue && _newKeysDown.Contains(entry.positiveButtonKeyCode.Value))
                    || (entry.negativeButtonKeyCode.HasValue && _newKeysDown.Contains(entry.negativeButtonKeyCode.Value))
                    || (entry.altPositiveButtonKeyCode.HasValue && _newKeysDown.Contains(entry.altPositiveButtonKeyCode.Value))
                    || (entry.altNegativeButtonKeyCode.HasValue && _newKeysDown.Contains(entry.altNegativeButtonKeyCode.Value)))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public static bool GetButtonUp(string buttonName)
        {
            if (IsPassthrough)
            {
                return UnityEngine.Input.GetButtonUp(buttonName);
            }
            else
            {
                foreach (var entry in _inputManagerSettings.GetEntriesByName(buttonName))
                {
                    if ((entry.positiveButtonKeyCode.HasValue && _newKeysUp.Contains(entry.positiveButtonKeyCode.Value))
                    || (entry.negativeButtonKeyCode.HasValue && _newKeysUp.Contains(entry.negativeButtonKeyCode.Value))
                    || (entry.altPositiveButtonKeyCode.HasValue && _newKeysUp.Contains(entry.altPositiveButtonKeyCode.Value))
                    || (entry.altNegativeButtonKeyCode.HasValue && _newKeysUp.Contains(entry.altNegativeButtonKeyCode.Value)))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
#endif