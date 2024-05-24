#if ENABLE_LEGACY_INPUT_MANAGER 
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RegressionGames.RGLegacyInputUtility
{
    public static class RGLegacyInputWrapper
    {
        private static MonoBehaviour _simulationContext;
        private static ISet<KeyCode> _keysHeld;
        private static ISet<KeyCode> _newKeysDown;
        private static ISet<KeyCode> _newKeysUp;
        private static Dictionary<KeyCode, Coroutine> _removeNewCoroutines;
        
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
            _removeNewCoroutines = new Dictionary<KeyCode, Coroutine>();
        }
        
        /**
         * Called by the test driver to release control of the user input.
         */
        public static void StopSimulation()
        {
            if (_simulationContext != null)
            {
                foreach (Coroutine coro in _removeNewCoroutines.Values)
                {
                    _simulationContext.StopCoroutine(coro);
                }

                _newKeysDown = null;
                _newKeysUp = null;
                _keysHeld = null;
                _removeNewCoroutines = null;
                _simulationContext = null;
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
            ClearKeyState(keyCode);
            _keysHeld.Add(keyCode);
            if (!wasHeld)
            {
                _newKeysDown.Add(keyCode);
                ScheduleRemoveNewKey(keyCode);
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
    }
}
#endif