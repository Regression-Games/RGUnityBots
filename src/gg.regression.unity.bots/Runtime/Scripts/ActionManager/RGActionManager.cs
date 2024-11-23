using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace RegressionGames.ActionManager
{
    public static class RGActionManager
    {
        // Using JSON files for storing configuration (instead of asset files) due to challenges faced with using ScriptableObject and AssetDatabase
        private static readonly string SETTINGS_DIRECTORY = "Assets/Resources";
        private static readonly string SETTINGS_NAME = "RGActionManagerSettings";
        private static readonly string SETTINGS_PATH = $"{SETTINGS_DIRECTORY}/{SETTINGS_NAME}.txt";

        private static MonoBehaviour _context;
        private static RGActionProvider _actionProvider;
        private static RGActionManagerSettings _settings;
        private static List<RGGameAction> _actions; // the set of actions after applying the user settings from RGActionManagerSettings

        /// <summary>
        /// The set of actions after applying the user settings in RGActionManagerSettings.
        /// </summary>
        public static IEnumerable<RGGameAction> Actions => _actions;

        /// <summary>
        /// Provides access to the original set of actions identified from the static analysis,
        /// prior to the user settings being applied from RGActionManagerSettings.
        /// </summary>
        public static IEnumerable<RGGameAction> OriginalActions => _actionProvider.Actions;

        public delegate void ActionsChangedHandler();
        public static event ActionsChangedHandler ActionsChanged;

        public static bool IsAvailable
        {
            get
            {
                if (_actionProvider == null)
                {
                    _actionProvider = new RGActionProvider();
                }
                return _actionProvider.IsAvailable;
            }
        }

        /// <summary>
        /// Provides access to the action manager settings.
        /// The settings should not change while a session is active.
        /// </summary>
        public static RGActionManagerSettings Settings => _settings;

        #if UNITY_EDITOR
        [InitializeOnLoadMethod]
        public static void InitializeInEditor()
        {
            _settings = LoadSettings();
            ReloadActions();
        }
        #endif

        private static RGActionManagerSettings LoadSettings()
        {

            string jsonText = null;
            #if UNITY_EDITOR
            if (File.Exists(SETTINGS_PATH))
            {
                using (StreamReader sr = new StreamReader(SETTINGS_PATH))
                {
                    jsonText = sr.ReadToEnd();
                }
            }
            #else
            {
                TextAsset jsonFile = Resources.Load<TextAsset>(SETTINGS_NAME);
                jsonText = jsonFile?.text;
            }
            #endif

            RGActionManagerSettings result = null;
            if (jsonText != null)
            {
                try
                {
                    result = JsonConvert.DeserializeObject<RGActionManagerSettings>(jsonText,
                        RGActionProvider.JSON_CONVERTERS);
                }
                catch (Exception e)
                {
                    RGDebug.LogWarning("Error reading action manager settings, reverting to defaults\n" + e.Message + "\n" + e.StackTrace);
                }
            }

            if (result == null || !result.IsValid())
            {
                result = new RGActionManagerSettings();
                #if UNITY_EDITOR
                SaveSettings(result);
                #endif
            }

            return result;
        }

        #if UNITY_EDITOR
        public static void SaveSettings(RGActionManagerSettings settings)
        {
            if (!Directory.Exists(SETTINGS_DIRECTORY))
            {
                Directory.CreateDirectory(SETTINGS_DIRECTORY);
            }
            using (StreamWriter sw = new StreamWriter(SETTINGS_PATH))
            {
                StringBuilder stringBuilder = new StringBuilder();
                settings.WriteToStringBuilder(stringBuilder);
                sw.Write(stringBuilder.ToString());
            }
        }
        #endif

        /// <summary>
        /// Returns whether the given context needs the game environment to be configured
        /// (starting input wrapper, hooking scenes, etc.)
        /// </summary>
        private static bool DoesContextNeedSetUp()
        {
            return _context is not BotSegmentsPlaybackController ;
        }

        public static bool TryStartSession(int segmentNumber, MonoBehaviour context, RGActionManagerSettings actionSettings = null)
        {
            try
            {
                StartSession(segmentNumber, context, actionSettings);
                return true;
            }
            catch (Exception ex)
            {
                RGDebug.LogWarning("Unable to start RGActionManager Session - " + ex);
                return false;
            }
        }

        /// <summary>
        /// Start an action manager session. This should be called prior to any calls to GetValidActions().
        /// </summary>
        /// <param name="segmentNumber">The bot sequence segment number</param>
        /// <param name="context">The MonoBehaviour context under which actions will be simulated.</param>
        /// <param name="actionSettings">Session-specific action settings (optional - if null will use saved configuration)</param>
        public static void StartSession(int segmentNumber, MonoBehaviour context, RGActionManagerSettings actionSettings = null)
        {
            if (_context != null)
            {
                throw new Exception($"Session is already active with context {_context}");
            }
            if (actionSettings != null)
            {
                _settings = actionSettings;
            }
            else
            {
                _settings = LoadSettings();
            }
            _context = context;

            ReloadActions();

            if (DoesContextNeedSetUp())
            {
                #if ENABLE_LEGACY_INPUT_MANAGER
                RGLegacyInputWrapper.StartSimulation(_context);
                #endif
                KeyboardEventSender.Initialize();
                SceneManager.sceneLoaded += OnSceneLoad;
                SceneManager.sceneUnloaded += OnSceneUnload;
                RGUtils.SetupOverrideEventSystem();
                RGUtils.ConfigureInputSettings();
            }

            InitInputState(segmentNumber);

            RGActionRuntimeCoverageAnalysis.StartRecording(segmentNumber);
        }

        /// <summary>
        /// Stop an action manager session.
        /// </summary>
        public static void StopSession()
        {
            if (_context != null)
            {
                if (DoesContextNeedSetUp())
                {
                    SceneManager.sceneLoaded -= OnSceneLoad;
                    SceneManager.sceneUnloaded -= OnSceneUnload;
                    #if ENABLE_LEGACY_INPUT_MANAGER
                    RGLegacyInputWrapper.StopSimulation();
                    #endif
                    RGUtils.TeardownOverrideEventSystem();
                    RGUtils.RestoreInputSettings();
                }
                _context = null;
                _settings = LoadSettings(); // restore settings back to the saved configuration

                RGActionRuntimeCoverageAnalysis.StopRecording();
            }
        }

        /// <summary>
        /// Computes the set of valid actions in the current game state as a dictionary that maps the actions to their instances.
        /// The dictionary and lists that are returned should NOT be modified or retained by the caller.
        /// </summary>
        /// <returns>
        /// A dictionary mapping actions to valid instances in the current state.
        /// The list of valid instances may be empty (i.e. the presence of an action as a key does not imply it has a valid instance in the current state).
        /// </returns>
        public static IEnumerable<IRGGameActionInstance> GetValidActions()
        {
            var tof = UnityEngine.Object.FindObjectOfType<TransformObjectFinder>();
            CurrentTransforms = tof.GetObjectStatusForCurrentFrame().Item2;

            var eventSystems = UnityEngine.Object.FindObjectsOfType<EventSystem>();
            CurrentEventSystems.Clear();
            CurrentEventSystems.AddRange(eventSystems);

            RGUtils.ForceApplicationFocus();

            foreach (RGGameAction action in Actions)
            {
                Debug.Assert(action.ParameterRange != null);

                // Fetch all objects of the target object type (use Resources API to support ANY type of loaded object)
                UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(action.ObjectType);

                foreach (var obj in objects)
                {
                    if (obj is Component { gameObject: { activeInHierarchy: false } })
                    {
                        // skip components that are on inactive game objects
                        continue;
                    }
                    if (obj is Behaviour { isActiveAndEnabled: false })
                    {
                        // skip disabled behaviours
                        continue;
                    }
                    if (action.IsValidForObject(obj))
                    {
                        yield return action.GetInstance(obj);
                    }
                }
            }
        }

        public static void ReloadActions()
        {
            var prevActionProvider = _actionProvider;
            _actionProvider = new RGActionProvider();
            if (_actionProvider.IsAvailable)
            {
                _actions = _settings.ApplySettings(_actionProvider.Actions);
            }
            else
            {
                _actions = null;
            }
            if (prevActionProvider != null)
            {
                ActionsChanged?.Invoke();
            }
        }

        private static void OnSceneUnload(Scene s)
        {
            RGUtils.TeardownOverrideEventSystem(s);
        }

        private static void OnSceneLoad(Scene s, LoadSceneMode m)
        {
            // configure the event system for simulated input whenever a new scene is loaded
            RGUtils.SetupOverrideEventSystem(s);
        }


        // Input simulation fields and methods used by the various action types

        private static Vector3 _mousePosition;
        private static Vector2 _mouseScroll;
        private static bool _leftMouseButton;
        private static bool _middleMouseButton;
        private static bool _rightMouseButton;
        private static bool _forwardMouseButton;
        private static bool _backMouseButton;
        public static Dictionary<long, ObjectStatus> CurrentTransforms { get; private set; }
        public static List<EventSystem> CurrentEventSystems { get; private set; }

        private static void InitInputState(int segmentNumber)
        {
            // move mouse off screen
            _mousePosition = MouseEventSender.MoveMouseOffScreen(segmentNumber);

            #if ENABLE_LEGACY_INPUT_MANAGER
            _mouseScroll = RGLegacyInputWrapper.mouseScrollDelta;
            _leftMouseButton = RGLegacyInputWrapper.GetKey(KeyCode.Mouse0);
            _middleMouseButton = RGLegacyInputWrapper.GetKey(KeyCode.Mouse2);
            _rightMouseButton = RGLegacyInputWrapper.GetKey(KeyCode.Mouse1);
            _forwardMouseButton = RGLegacyInputWrapper.GetKey(KeyCode.Mouse3);
            _backMouseButton = RGLegacyInputWrapper.GetKey(KeyCode.Mouse4);
            #else
            _mouseScroll = Mouse.current.delta.value;
            _leftMouseButton = Mouse.current.leftButton.isPressed;
            _middleMouseButton = Mouse.current.middleButton.isPressed;
            _rightMouseButton = Mouse.current.rightButton.isPressed;
            _forwardMouseButton = Mouse.current.forwardButton.isPressed;
            _backMouseButton = Mouse.current.backButton.isPressed;
            #endif

            CurrentEventSystems = new List<EventSystem>();
        }

        public static void SimulateKeyState(int segmentNumber, KeyCode keyCode, bool isPressed)
        {
            if (keyCode >= KeyCode.Mouse0 && keyCode <= KeyCode.Mouse6)
            {
                bool leftButton = keyCode == KeyCode.Mouse0 ? isPressed : _leftMouseButton;
                bool middleButton = keyCode == KeyCode.Mouse2 ? isPressed : _middleMouseButton;
                bool rightButton = keyCode == KeyCode.Mouse1 ? isPressed : _rightMouseButton;
                bool forwardButton =
                    keyCode == KeyCode.Mouse3 ? isPressed : _forwardMouseButton;
                bool backButton =
                    keyCode == KeyCode.Mouse4 ? isPressed : _backMouseButton;
                MouseEventSender.SendRawPositionMouseEvent(segmentNumber,
                    _mousePosition,
                    leftButton: leftButton, middleButton: middleButton, rightButton: rightButton,
                    forwardButton: forwardButton, backButton: backButton, scroll: _mouseScroll);
                _leftMouseButton = leftButton;
                _middleMouseButton = middleButton;
                _rightMouseButton = rightButton;
                _forwardMouseButton = forwardButton;
                _backMouseButton = backButton;
            }
            else
            {
                Key key = RGLegacyInputUtils.KeyCodeToInputSystemKey(keyCode);
                SimulateKeyState(segmentNumber, key, isPressed);
            }
        }

        public static void SimulateKeyState(int segmentNumber, Key key, bool isPressed)
        {
            if (key != Key.None)
            {
                KeyboardEventSender.SendKeyEvent(segmentNumber, key, isPressed ? KeyState.Down : KeyState.Up);
            }
        }

        public static void SimulateMouseMovement(int segmentNumber, Vector2 mousePosition)
        {
            MouseEventSender.SendRawPositionMouseEvent(segmentNumber, mousePosition, leftButton: _leftMouseButton, middleButton: _middleMouseButton,
                rightButton: _rightMouseButton, forwardButton: _forwardMouseButton, backButton: _backMouseButton, scroll: _mouseScroll);
            _mousePosition = mousePosition;
        }

        public static void SimulateMouseMovementDelta(int segmentNumber, Vector2 mousePositionDelta)
        {
            Vector3 delta = mousePositionDelta;
            SimulateMouseMovement(segmentNumber, _mousePosition + delta);
        }

        public static void SimulateMouseScroll(int segmentNumber, Vector2 mouseScroll)
        {
            MouseEventSender.SendRawPositionMouseEvent(segmentNumber, _mousePosition, leftButton: _leftMouseButton,
                middleButton: _middleMouseButton,
                rightButton: _rightMouseButton, forwardButton: _forwardMouseButton, backButton: _backMouseButton,
                scroll: mouseScroll);
            _mouseScroll = mouseScroll;
        }

        public static void SimulateMouseButton(int segmentNumber, int mouseButton, bool isPressed)
        {
            SimulateKeyState(segmentNumber, KeyCode.Mouse0 + mouseButton, isPressed);
        }
    }
}
