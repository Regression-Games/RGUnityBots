using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
        private static IRGActionProvider _actionProvider;
        private static RGActionManagerSettings _settings;
        private static Dictionary<RGGameAction, IList<IRGGameActionInstance>> _sessionActionsBuf;

        /// <summary>
        /// Provides access to the actions obtained from the action provider.
        /// This property does not consider whether actions are disabled in the settings:
        /// the IsActionEnabled method can be used to determine this.
        /// </summary>
        public static IEnumerable<RGGameAction> Actions => _actionProvider.Actions;

        public delegate void ActionsChangedHandler();
        public static event ActionsChangedHandler ActionsChanged;

        public static bool IsAvailable => _actionProvider != null;

        /// <summary>
        /// Provides access to the action manager settings.
        /// The settings should not change while a session is active.
        /// </summary>
        public static RGActionManagerSettings Settings => _settings;
        
        #if UNITY_EDITOR
        [InitializeOnLoadMethod]
        public static void InitializeInEditor()
        {
            // find the action provider
            var actionProviders = TypeCache.GetTypesDerivedFrom<IRGActionProvider>();
            Type providerType = actionProviders.FirstOrDefault();
            if (providerType != null)
            {
                IRGActionProvider provider = (IRGActionProvider)providerType.GetConstructor(Type.EmptyTypes).Invoke(null);
                SetActionProvider(provider);
            }
            LoadSettings();
        }
        #endif

        private static void LoadSettings()
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
            if (jsonText != null)
            {
                _settings = JsonUtility.FromJson<RGActionManagerSettings>(jsonText);
            }
            else
            {
                _settings = new RGActionManagerSettings();
                #if UNITY_EDITOR
                SaveSettings();
                #endif
            }
        }

        #if UNITY_EDITOR
        public static void SaveSettings()
        {
            if (!Directory.Exists(SETTINGS_DIRECTORY))
            {
                Directory.CreateDirectory(SETTINGS_DIRECTORY);
            }
            using (StreamWriter sw = new StreamWriter(SETTINGS_PATH))
            {
                sw.Write(JsonUtility.ToJson(_settings));
            }
        }
        #endif

        public static void SetActionProvider(IRGActionProvider actionProvider)
        {
            _actionProvider = actionProvider;
            _actionProvider.ActionsChanged += OnActionsChanged;
        }

        private static void OnActionsChanged(object sender, EventArgs args)
        {
            ActionsChanged?.Invoke();
        }

        public static void StartSession(MonoBehaviour context)
        {
            if (_actionProvider == null)
            {
                throw new Exception("Must set an action provider before starting a session");
            }
            if (_context != null)
            {
                throw new Exception($"Session is already active with context {_context}");
            }
            LoadSettings();
            _context = context;
            
            _sessionActionsBuf = new Dictionary<RGGameAction, IList<IRGGameActionInstance>>();
            foreach (RGGameAction action in _actionProvider.Actions)
            {
                if (IsActionEnabled(action))
                {
                    _sessionActionsBuf.Add(action, new List<IRGGameActionInstance>());
                }
            }
            
            RGLegacyInputWrapper.StartSimulation(_context);
            SceneManager.sceneLoaded += OnSceneLoad;
            RGUtils.SetupEventSystem();
            RGUtils.ConfigureInputSettings();
            InitInputState();
        }

        public static void StopSession()
        {
            if (_context != null)
            {
                SceneManager.sceneLoaded -= OnSceneLoad;
                RGLegacyInputWrapper.StopSimulation();
                RGUtils.RestoreInputSettings();
                _sessionActionsBuf = null;
                _context = null;
            }
        }

        public static bool IsActionEnabled(RGGameAction action)
        {
            if (action.Paths.All(path => !_settings.IsActionEnabled(path)))
            {
                // action is disabled in settings
                return false;
            }
            return true;
        }

        /// <summary>
        /// Computes the set of valid actions in the current game state as a dictionary that maps the actions to their instances.
        /// The dictionary and lists that are returned should NOT be modified or retained by the caller.
        /// </summary>
        /// <returns>
        /// A dictionary mapping actions to valid instances in the current state.
        /// The list of valid instances may be empty (i.e. the presence of an action as a key does not imply it has a valid instance in the current state).
        /// </returns>
        public static IDictionary<RGGameAction, IList<IRGGameActionInstance>> GetValidActions()
        {
            CurrentUITransforms = InGameObjectFinder.GetInstance().GetUITransformsForCurrentFrame().Item2;
            CurrentGameObjectTransforms = InGameObjectFinder.GetInstance().GetGameObjectTransformsForCurrentFrame().Item2;
            RGUtils.ForceApplicationFocus();
            foreach (var entry in _sessionActionsBuf)
            {
                RGGameAction action = entry.Key;
                IList<IRGGameActionInstance> actionInstances = entry.Value;
                actionInstances.Clear();
                
                Debug.Assert(action.ParameterRange != null);
                UnityEngine.Object[] objects = UnityEngine.Object.FindObjectsOfType(action.ObjectType);
                foreach (var obj in objects)
                {
                    if (obj is Component c && !c.gameObject.activeInHierarchy)
                    {
                        // skip components that are on inactive game objects
                        continue;
                    }
                    if (obj is Behaviour b && !b.isActiveAndEnabled)
                    {
                        // skip disabled behaviours
                        continue;
                    }
                    if (action.IsValidForObject(obj))
                    {
                        actionInstances.Add(action.GetInstance(obj));
                    }
                }
            }
            return _sessionActionsBuf;
        }

        private static void OnSceneLoad(Scene s, LoadSceneMode m)
        {
            // configure the event system for simulated input whenever a new scene is loaded
            RGUtils.SetupEventSystem();
        }
        
        
        
        // Input simulation fields and methods used by the various action types

        private static Vector3 _mousePosition;
        private static Vector2 _mouseScroll;
        private static bool _leftMouseButton;
        private static bool _middleMouseButton;
        private static bool _rightMouseButton;
        private static bool _forwardMouseButton;
        private static bool _backMouseButton;
        public static Dictionary<int, TransformStatus> CurrentUITransforms { get; private set; }
        public static Dictionary<int, TransformStatus> CurrentGameObjectTransforms { get; private set; }

        private static void InitInputState()
        {
            _mousePosition = RGLegacyInputWrapper.mousePosition;
            _mouseScroll = RGLegacyInputWrapper.mouseScrollDelta;
            _leftMouseButton = RGLegacyInputWrapper.GetKey(KeyCode.Mouse0);
            _middleMouseButton = RGLegacyInputWrapper.GetKey(KeyCode.Mouse2);
            _rightMouseButton = RGLegacyInputWrapper.GetKey(KeyCode.Mouse1);
            _forwardMouseButton = RGLegacyInputWrapper.GetKey(KeyCode.Mouse3);
            _backMouseButton = RGLegacyInputWrapper.GetKey(KeyCode.Mouse4);
            
            // move mouse off screen
            MouseEventSender.SendRawPositionMouseEvent(-1, new Vector2(Screen.width+20, -20));
        }

        public static void SimulateKeyState(KeyCode keyCode, bool isPressed)
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
                MouseEventSender.SendRawPositionMouseEvent(0,
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
                SimulateKeyState(key, isPressed);
            }
        }

        public static void SimulateKeyState(Key key, bool isPressed)
        {
            if (key != Key.None)
            {
                KeyControl control = Keyboard.current[key];
                KeyboardInputActionData data = new KeyboardInputActionData()
                {
                    action = control.name,
                    binding = control.path,
                    startTime = Time.unscaledTime,
                    endTime = isPressed ? null : Time.unscaledTime
                };
                KeyboardEventSender.SendKeyEvent(0, data, isPressed ? KeyState.Down : KeyState.Up);
            }
        }

        public static void SimulateMouseMovement(Vector2 mousePosition)
        {
            MouseEventSender.SendRawPositionMouseEvent(0, mousePosition, leftButton: _leftMouseButton, middleButton: _middleMouseButton, 
                rightButton: _rightMouseButton, forwardButton: _forwardMouseButton, backButton: _backMouseButton, scroll: _mouseScroll);
            _mousePosition = mousePosition;
        }

        public static void SimulateMouseMovementDelta(Vector2 mousePositionDelta)
        {
            Vector3 delta = mousePositionDelta;
            SimulateMouseMovement(_mousePosition + delta);
        }

        public static void SimulateMouseScroll(Vector2 mouseScroll)
        {
            MouseEventSender.SendRawPositionMouseEvent(0, _mousePosition, leftButton: _leftMouseButton,
                middleButton: _middleMouseButton,
                rightButton: _rightMouseButton, forwardButton: _forwardMouseButton, backButton: _backMouseButton,
                scroll: mouseScroll);
            _mouseScroll = mouseScroll;
        }

        public static void SimulateLeftMouseButton(bool isPressed)
        {
            SimulateKeyState(KeyCode.Mouse0, isPressed);
        }
        
        public static void SimulateMiddleMouseButton(bool isPressed)
        {
            SimulateKeyState(KeyCode.Mouse2, isPressed);
        }
        
        public static void SimulateRightMouseButton(bool isPressed)
        {
            SimulateKeyState(KeyCode.Mouse1, isPressed);
        }
        
        public static void SimulateForwardMouseButton(bool isPressed)
        {
            SimulateKeyState(KeyCode.Mouse3, isPressed);
        }
        
        public static void SimulateBackMouseButton(bool isPressed)
        {
            SimulateKeyState(KeyCode.Mouse4, isPressed);
        }
    }
}