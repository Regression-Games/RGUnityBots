﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.StateRecorder;
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
        /// The set of actions after applying the settings 
        /// </summary>
        public static IEnumerable<RGGameAction> Actions => _actions;
        
        /// <summary>
        /// Provides access to the original set of actions identified via the static analysis,
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
            ReloadActions();
            _settings = LoadSettings();
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
                result = JsonUtility.FromJson<RGActionManagerSettings>(jsonText);
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
            return _context is not ReplayDataPlaybackController;
        }

        /// <summary>
        /// Start an action manager session. This should be called prior to any calls to GetValidActions().
        /// </summary>
        /// <param name="context">The MonoBehaviour context under which actions will be simulated.</param>
        /// <param name="actionSettings">Session-specific action settings (optional - if null will use saved configuration)</param>
        public static void StartSession(MonoBehaviour context, RGActionManagerSettings actionSettings = null)
        {
            if (_context != null)
            {
                throw new Exception($"Session is already active with context {_context}");
            }
            ReloadActions();
            if (actionSettings != null)
            {
                _settings = actionSettings;
            }
            else
            {
                _settings = LoadSettings();
            }
            _context = context;
            _sessionActions = new List<RGGameAction>(_actionProvider.Actions.Where(IsActionEnabled));

            if (DoesContextNeedSetUp())
            {
                #if ENABLE_LEGACY_INPUT_MANAGER
                RGLegacyInputWrapper.StartSimulation(_context);
                #endif
                KeyboardEventSender.Initialize();
                SceneManager.sceneLoaded += OnSceneLoad;
                RGUtils.SetupEventSystem();
                RGUtils.ConfigureInputSettings();
            }
            
            InitInputState();
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
                    #if ENABLE_LEGACY_INPUT_MANAGER
                    RGLegacyInputWrapper.StopSimulation();
                    #endif
                    RGUtils.RestoreInputSettings();
                }
                _sessionActions = null;
                _context = null;
                _settings = LoadSettings(); // restore settings back to the saved configuration 
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
            
            foreach (RGGameAction action in _sessionActions)
            {
                Debug.Assert(action.ParameterRange != null);
                
                // Fetch all objects of the target object type (use Resources API to support ANY type of loaded object)
                UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(action.ObjectType);
                
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

        public static void ReloadActions()
        {
            var prevActionProvider = _actionProvider;
            _actionProvider = new RGActionProvider();
            if (prevActionProvider != null)
            {
                ActionsChanged?.Invoke();
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
        public static Dictionary<long, ObjectStatus> CurrentTransforms { get; private set; }
        public static List<EventSystem> CurrentEventSystems { get; private set; }

        private static void InitInputState()
        {
            // move mouse off screen
            var mousePos = new Vector2(Screen.width + 20, -20);
            MouseEventSender.SendRawPositionMouseEvent(-1, mousePos);

            _mousePosition = mousePos;
            
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
                KeyboardEventSender.SendKeyEvent(0, key, isPressed ? KeyState.Down : KeyState.Up);
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

        public static void SimulateMouseButton(int mouseButton, bool isPressed)
        {
            SimulateKeyState(KeyCode.Mouse0 + mouseButton, isPressed);
        }
    }
}