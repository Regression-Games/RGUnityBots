#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RegressionGames.RGLegacyInputUtility
{
    /// <summary>
    /// Update mode of RGLegacyInputWrapper.
    /// Default is AUTOMATIC.
    /// </summary>
    public enum RGLegacyInputUpdateMode
    {
        AUTOMATIC,
        MANUAL
    }

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

    enum RGLegacySimulatedInputEventType
    {
        KEY_EVENT,
        MOUSE_MOVEMENT_EVENT,
        MOUSE_SCROLL_EVENT
    }

    struct RGLegacySimulatedInputEvent
    {
        public RGLegacySimulatedInputEventType eventType;

        // Key event
        public KeyCode keyCode;
        public bool isKeyPressed;

        // Mouse movement event
        public Vector3 newMousePosition;
        public Vector3? newMouseDelta;

        // Mouse scroll event
        public Vector2 newMouseScrollDelta;
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
        private static Camera[] _camerasBuf;

        private static (GameObject, Camera)? _currentHitObject;
        private static (GameObject, Camera)? _lastHitObject;
        private static (GameObject, Camera)? _mouseDownObject;

        private static (GameObject, Camera)? _currentHitObject2D;
        private static (GameObject, Camera)? _lastHitObject2D;
        private static (GameObject, Camera)? _mouseDownObject2D;

        private static RGLegacyInputManagerSettings _inputManagerSettings;
        private static Coroutine _autoUpdateLoopCoro;
        private static Queue<RGLegacySimulatedInputEvent> _inputSimEventQueue;

        private static RGLegacyInputUpdateMode _updateMode = RGLegacyInputUpdateMode.AUTOMATIC;

        /// <summary>
        /// If updateMode is AUTOMATIC (the default), then the event processing is automatically scheduled every frame.
        /// Otherwise, if updateMode is MANUAL, then the application must call RGLegacyInputWrapper.Update() manually on a regular interval.
        /// </summary>
        public static RGLegacyInputUpdateMode UpdateMode
        {
            get => _updateMode;
            set
            {
                _updateMode = value;
                if (_updateMode == RGLegacyInputUpdateMode.AUTOMATIC)
                {
                    if (_simulationContext != null && _autoUpdateLoopCoro == null)
                    {
                        _autoUpdateLoopCoro = _simulationContext.StartCoroutine(AutoUpdateLoop());
                    }
                } else if (_updateMode == RGLegacyInputUpdateMode.MANUAL)
                {
                    if (_simulationContext != null && _autoUpdateLoopCoro != null)
                    {
                        _simulationContext.StopCoroutine(_autoUpdateLoopCoro);
                        _autoUpdateLoopCoro = null;
                    }
                }
            }
        }

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
         * Returns the current input manager settings.
         * If a simulation is not active, this will return null.
         */
        public static RGLegacyInputManagerSettings InputManagerSettings => _inputManagerSettings;

        /// <summary>
        /// Called by the test driver to take control of the user input.
        /// Context parameter is the driving MonoBehaviour to use as context.
        /// </summary>
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
            _lastHitObject = null;
            _currentHitObject = null;
            _mouseDownObject = null;
            _lastHitObject2D = null;
            _currentHitObject2D = null;
            _mouseDownObject2D = null;
            _inputManagerSettings = new RGLegacyInputManagerSettings();
            _inputSimEventQueue = new Queue<RGLegacySimulatedInputEvent>();

            // initialize axis states
            foreach (var entry in _inputManagerSettings.Entries)
            {
                _axisStates[entry] = new RGLegacyAxisInputState();
            }

            if (UpdateMode == RGLegacyInputUpdateMode.AUTOMATIC)
            {
                // start event processing loop
                _autoUpdateLoopCoro = _simulationContext.StartCoroutine(AutoUpdateLoop());
            }
        }

        /**
         * Called by the test driver to release control of the user input.
         */
        public static void StopSimulation()
        {
            if (_simulationContext != null)
            {
                if (_autoUpdateLoopCoro != null)
                {
                    _simulationContext.StopCoroutine(_autoUpdateLoopCoro);
                    _autoUpdateLoopCoro = null;
                }
                _inputSimEventQueue = null;
                _inputManagerSettings = null;
                _newKeysDown = null;
                _newKeysUp = null;
                _axisStates = null;
                _lastHitObject = null;
                _currentHitObject = null;
                _mouseDownObject = null;
                _lastHitObject2D = null;
                _currentHitObject2D = null;
                _mouseDownObject2D = null;
                _keysHeld = null;
                _simulationContext = null;
            }
        }

        /// <summary>
        /// Perform an update step of input event processing.
        /// This should only be called by the application if UpdateMode is MANUAL.
        /// </summary>
        public static void Update()
        {
            // Clear all "new" flags and deltas
            _newKeysDown.Clear();
            _newKeysUp.Clear();
            _anyKeyDown = false;
            _mousePosDelta = Vector3.zero;
            _mouseScrollDelta = Vector2.zero;

            // Process simulated input event queue
            while (_inputSimEventQueue.TryDequeue(out RGLegacySimulatedInputEvent evt))
            {
                switch (evt.eventType)
                {
                    case RGLegacySimulatedInputEventType.KEY_EVENT:
                    {
                        KeyCode keyCode = evt.keyCode;
                        bool isPressed = evt.isKeyPressed;
                        bool wasHeld = _keysHeld.Contains(keyCode);
                        if (isPressed)
                        {
                            bool anyHeld = _keysHeld.Count > 0;
                            _keysHeld.Add(keyCode);
                            if (!wasHeld)
                            {
                                _newKeysDown.Add(keyCode);
                            }
                            if (!anyHeld)
                            {
                                _anyKeyDown = true;
                            }
                        }
                        else
                        {
                            _keysHeld.Remove(keyCode);
                            if (wasHeld)
                            {
                                _newKeysUp.Add(keyCode);
                            }
                        }
                        break;
                    }
                    case RGLegacySimulatedInputEventType.MOUSE_MOVEMENT_EVENT:
                    {
                        Vector3 newMousePosition = evt.newMousePosition;
                        if (evt.newMouseDelta.HasValue)
                        {
                            _mousePosDelta = evt.newMouseDelta.Value;
                        }
                        else
                        {
                            _mousePosDelta = newMousePosition - _mousePosition;
                        }
                        _mousePosition = newMousePosition;
                        break;
                    }
                    case RGLegacySimulatedInputEventType.MOUSE_SCROLL_EVENT:
                    {
                        Vector2 newMouseScrollDelta = evt.newMouseScrollDelta;
                        _mouseScrollDelta = newMouseScrollDelta;
                        break;
                    }
                }
            }

            // Update axis states based on input
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

            // Invoke the appropriate mouse event handlers on MonoBehaviours
            {
                int numCameras = Camera.allCamerasCount;
                if (_camerasBuf == null || _camerasBuf.Length != numCameras)
                {
                    _camerasBuf = new Camera[numCameras];
                }

                Camera.GetAllCameras(_camerasBuf);
                foreach (Camera camera in _camerasBuf)
                {
                    if (camera == null || camera.eventMask == 0 || camera.targetTexture != null)
                    {
                        continue;
                    }

                    var cameraRaycastMask = camera.cullingMask & camera.eventMask;

                    // 3D raycast
                    {
                        Ray mouseRay = camera.ScreenPointToRay(_mousePosition);
                        if (Physics.Raycast(mouseRay, out RaycastHit hit, maxDistance: Mathf.Infinity,
                                layerMask: cameraRaycastMask))
                        {
                            _currentHitObject = (hit.collider.gameObject, camera);
                        }
                    }

                    // 2D raycast
                    {
                        Vector3 mouseWorldPt = camera.ScreenToWorldPoint(new Vector3(_mousePosition.x, _mousePosition.y, camera.nearClipPlane));
                        var collider = Physics2D.OverlapPoint(mouseWorldPt, cameraRaycastMask);
                        if (collider != null)
                        {
                            _currentHitObject2D = (collider.gameObject, camera);
                        }
                    }
                }

                // 3D collider mouse events are always handled before 2D collider mouse events
                InvokeMouseEventHandlers(_currentHitObject, _lastHitObject, ref _mouseDownObject);
                InvokeMouseEventHandlers(_currentHitObject2D, _lastHitObject2D, ref _mouseDownObject2D);
                _lastHitObject = _currentHitObject;
                _lastHitObject2D = _currentHitObject2D;
                _currentHitObject = null;
                _currentHitObject2D = null;
            }

        }

        public static bool IsLeftMouseButtonPointerCurrentlyOverGameObject()
        {
            if (anyKeyDown &&
                (_keysHeld.Contains(KeyCode.Mouse0)
                 || _keysHeld.Contains(KeyCode.Mouse1)
                 || _keysHeld.Contains(KeyCode.Mouse2)
                 || _keysHeld.Contains(KeyCode.Mouse3)
                 || _keysHeld.Contains(KeyCode.Mouse4)
                 || _keysHeld.Contains(KeyCode.Mouse5)
                 || _keysHeld.Contains(KeyCode.Mouse6))
               )
            {
                return _currentHitObject != null || _currentHitObject2D != null;
            }

            return false;
        }

        /// <summary>
        /// Coroutine that automatically performs an update every frame.
        /// This coroutine is run on the object that is given as the context parameter to StartSimulation().
        /// This is only used if the update mode given was AUTOMATIC.
        /// </summary>
        private static IEnumerator AutoUpdateLoop()
        {
            while(_simulationContext!=null)
            {
                Update();
                yield return null;
            }
        }

        private static void SendMouseEventMessage(GameObject gameObject, string methodName)
        {
            if (gameObject != null)
            {
                gameObject.SendMessage(methodName, null, SendMessageOptions.DontRequireReceiver);
            }
        }

        private static void InvokeMouseEventHandlers((GameObject, Camera)? hitObject,
            (GameObject, Camera)? lastHitObject, ref (GameObject, Camera)? mouseDownObject)
        {
            bool leftButton = _keysHeld.Contains(KeyCode.Mouse0);
            bool leftButtonDown = _newKeysDown.Contains(KeyCode.Mouse0);

            if (leftButtonDown)
            {
                if (hitObject.HasValue)
                {
                    mouseDownObject = hitObject;
                    SendMouseEventMessage(mouseDownObject.Value.Item1, "OnMouseDown");
                }
            }
            else if (!leftButton)
            {
                if (mouseDownObject.HasValue)
                {
                    if (mouseDownObject == hitObject)
                    {
                        SendMouseEventMessage(mouseDownObject.Value.Item1, "OnMouseUpAsButton");
                    }
                    SendMouseEventMessage(mouseDownObject.Value.Item1, "OnMouseUp");
                    mouseDownObject = null;
                }
            }
            else if (mouseDownObject.HasValue)
            {
                SendMouseEventMessage(mouseDownObject.Value.Item1, "OnMouseDrag");
            }

            if (hitObject == lastHitObject)
            {
                if (hitObject.HasValue)
                {
                    SendMouseEventMessage(hitObject.Value.Item1, "OnMouseOver");
                }
            }
            else
            {
                if (lastHitObject.HasValue)
                {
                    SendMouseEventMessage(lastHitObject.Value.Item1, "OnMouseExit");
                }
                if (hitObject.HasValue)
                {
                    SendMouseEventMessage(hitObject.Value.Item1, "OnMouseEnter");
                    SendMouseEventMessage(hitObject.Value.Item1, "OnMouseOver");
                }
            }
        }

        /**
         * Called by the test driver to simulate a key press.
         */
        public static void SimulateKeyPress(KeyCode keyCode)
        {
            RGLegacySimulatedInputEvent evt = new RGLegacySimulatedInputEvent();
            evt.eventType = RGLegacySimulatedInputEventType.KEY_EVENT;
            evt.keyCode = keyCode;
            evt.isKeyPressed = true;
            _inputSimEventQueue.Enqueue(evt);
        }

        /**
         * Called by the test driver to simulate a key release.
         */
        public static void SimulateKeyRelease(KeyCode keyCode)
        {
            RGLegacySimulatedInputEvent evt = new RGLegacySimulatedInputEvent();
            evt.eventType = RGLegacySimulatedInputEventType.KEY_EVENT;
            evt.keyCode = keyCode;
            evt.isKeyPressed = false;
            _inputSimEventQueue.Enqueue(evt);
        }

        /**
         * Called by the test driver to simulate a mouse movement to a new position.
         * Delta can be optionally specified. Otherwise it is inferred from the previous mouse position.
         */
        public static void SimulateMouseMovement(Vector3 newMousePosition, Vector3? newMouseDelta = null)
        {
            RGLegacySimulatedInputEvent evt = new RGLegacySimulatedInputEvent();
            evt.eventType = RGLegacySimulatedInputEventType.MOUSE_MOVEMENT_EVENT;
            evt.newMousePosition = newMousePosition;
            evt.newMouseDelta = newMouseDelta;
            _inputSimEventQueue.Enqueue(evt);
        }

        public static void SimulateMouseScrollWheel(Vector2 newMouseScrollDelta)
        {
            RGLegacySimulatedInputEvent evt = new RGLegacySimulatedInputEvent();
            evt.eventType = RGLegacySimulatedInputEventType.MOUSE_SCROLL_EVENT;
            evt.newMouseScrollDelta = newMouseScrollDelta;
            _inputSimEventQueue.Enqueue(evt);
        }

        private static Regex _joystickButtonPattern = new Regex(@"joystick (\d+) button (\d+)", RegexOptions.Compiled);

        // Based on https://docs.unity3d.com/Manual/class-InputManager.html
        public static KeyCode KeyNameToCode(string buttonName)
        {
            if (buttonName.StartsWith("joystick button"))
            {
                return (KeyCode)Enum.Parse(typeof(KeyCode), "JoystickButton" + int.Parse(buttonName.Replace("joystick button", "").Trim()));
            }

            if (buttonName.StartsWith("mouse "))
            {
                return KeyCode.Mouse0 + int.Parse(buttonName.Substring(6).Trim());
            }

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
                    return Input.anyKey;
                }

                return _keysHeld.Count > 0;
            }
        }

        public static bool anyKeyDown
        {
            get
            {
                if (IsPassthrough)
                {
                    return Input.anyKeyDown;
                }

                return _anyKeyDown;
            }
        }

        public static bool mousePresent
        {
            get
            {
                if (IsPassthrough)
                {
                    return Input.mousePresent;
                }

                return true;
            }
        }

        public static Vector3 mousePosition
        {
            get
            {
                if (IsPassthrough)
                {
                    return Input.mousePosition;
                }

                return _mousePosition;
            }
        }

        public static Vector2 mouseScrollDelta
        {
            get
            {
                if (IsPassthrough)
                {
                    return Input.mouseScrollDelta;
                }

                return _mouseScrollDelta;
            }
        }

        public static bool GetKey(string name)
        {
            if (IsPassthrough)
            {
                return Input.GetKey(name);
            }

            return _keysHeld.Contains(KeyNameToCode(name));
        }

        public static bool GetKey(KeyCode key)
        {
            if (IsPassthrough)
            {
                return Input.GetKey(key);
            }

            return _keysHeld.Contains(key);
        }

        public static bool GetKeyDown(string name)
        {
            if (IsPassthrough)
            {
                return Input.GetKeyDown(name);
            }

            return _newKeysDown.Contains(KeyNameToCode(name));
        }

        public static bool GetKeyDown(KeyCode key)
        {
            if (IsPassthrough)
            {
                return Input.GetKeyDown(key);
            }

            return _newKeysDown.Contains(key);
        }

        public static bool GetKeyUp(string name)
        {
            if (IsPassthrough)
            {
                return Input.GetKeyUp(name);
            }

            return _newKeysUp.Contains(KeyNameToCode(name));
        }

        public static bool GetKeyUp(KeyCode key)
        {
            if (IsPassthrough)
            {
                return Input.GetKeyUp(key);
            }

            return _newKeysUp.Contains(key);
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
                return Input.GetMouseButton(button);
            }

            return GetKey(MouseButtonToKeyCode(button));
        }

        public static bool GetMouseButtonDown(int button)
        {
            if (IsPassthrough)
            {
                return Input.GetMouseButtonDown(button);
            }

            return GetKeyDown(MouseButtonToKeyCode(button));
        }

        public static bool GetMouseButtonUp(int button)
        {
            if (IsPassthrough)
            {
                return Input.GetMouseButtonUp(button);
            }

            return GetKeyUp(MouseButtonToKeyCode(button));
        }

        public static float GetAxisRaw(string name)
        {
            if (IsPassthrough)
            {
                return Input.GetAxisRaw(name);
            }

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

        public static float GetAxis(string name)
        {
            if (IsPassthrough)
            {
                return Input.GetAxis(name);
            }

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

        public static bool GetButton(string buttonName)
        {
            if (IsPassthrough)
            {
                return Input.GetButton(buttonName);
            }

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

        public static bool GetButtonDown(string buttonName)
        {
            if (IsPassthrough)
            {
                return Input.GetButtonDown(buttonName);
            }

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

        public static bool GetButtonUp(string buttonName)
        {
            if (IsPassthrough)
            {
                return Input.GetButtonUp(buttonName);
            }

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
#endif
