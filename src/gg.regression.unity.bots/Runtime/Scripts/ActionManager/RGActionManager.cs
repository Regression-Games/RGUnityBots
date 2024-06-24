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
using UnityEditor;
using UnityEngine.Windows;
#endif

namespace RegressionGames.ActionManager
{
    public static class RGActionManager
    {
        private static readonly string SETTINGS_DIRECTORY = "Assets/Resources";
        private static readonly string SETTINGS_RESOURCE_NAME = "RGActionManagerSettings";
        private static readonly string SETTINGS_RESOURCE_PATH = $"{SETTINGS_DIRECTORY}/{SETTINGS_RESOURCE_NAME}.asset";
            
        private static MonoBehaviour _context;
        private static IRGActionProvider _actionProvider;
        private static RGActionManagerSettings _settings;
        private static List<RGGameAction> _sessionActions;

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
                LoadSettings();
            }
        }
        #endif

        private static void LoadSettings()
        {
            _settings = Resources.Load<RGActionManagerSettings>(SETTINGS_RESOURCE_NAME);
            if (_settings == null)
            {
                _settings = ScriptableObject.CreateInstance<RGActionManagerSettings>();
                #if UNITY_EDITOR
                Directory.CreateDirectory(SETTINGS_DIRECTORY);
                AssetDatabase.CreateAsset(_settings, SETTINGS_RESOURCE_PATH);
                AssetDatabase.SaveAssets();
                #endif
            }
        }

        #if UNITY_EDITOR
        public static void SaveSettings()
        {
            GUID guid = AssetDatabase.GUIDFromAssetPath(SETTINGS_RESOURCE_PATH);
            AssetDatabase.SaveAssetIfDirty(guid);
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
            
            _sessionActions = new List<RGGameAction>();
            foreach (RGGameAction action in _actionProvider.Actions)
            {
                if (IsActionEnabled(action))
                {
                    _sessionActions.Add(action);
                }
            }
            
            RGLegacyInputWrapper.StartSimulation(_context);
            SceneManager.sceneLoaded += OnSceneLoad;
            RGUtils.SetupEventSystem();
            InitInputState();
        }

        public static void StopSession()
        {
            if (_context != null)
            {
                SceneManager.sceneLoaded -= OnSceneLoad;
                RGLegacyInputWrapper.StopSimulation();
                _sessionActions = null;
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

        public static IEnumerable<IRGGameActionInstance> GetValidActions()
        {
            CurrentUITransforms = InGameObjectFinder.GetInstance().GetUITransformsForCurrentFrame().Item2;
            CurrentGameObjectTransforms = InGameObjectFinder.GetInstance().GetGameObjectTransformsForCurrentFrame().Item2;
            foreach (RGGameAction action in _sessionActions)
            {
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
                        yield return action.GetInstance(obj);
                    }
                }
            }
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