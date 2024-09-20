using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RegressionGames.RGLegacyInputUtility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RegressionGames
{
    public static class RGUtils
    {
        public static bool IsCSharpPrimitive(string typeName)
        {
            HashSet<string> primitiveTypes = new HashSet<string>
            {
                "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint", "long", "ulong", "short", "ushort",
                "string"
            };

            return primitiveTypes.Contains(typeName);
        }

        /// <summary>
        /// Gets the latest write date for any file at the specified path OR any file in the specified directory.
        /// </summary>
        /// <remarks>
        /// Normally, fetching the last write date for a directory will only show you when the directory metadata was changed.
        /// However, if given a directory, this method will recursively search that directory for the latest write date of any file in the directory.
        /// </remarks>
        /// <param name="path">The path to fetch the last updated date for. Can be a file or directory.</param>
        /// <returns>The latest write date for any file in the specified path. Or, <c>null</c> if the path does not exist.</returns>
        public static DateTimeOffset? GetLatestWriteDate(string path)
        {
            // If it's a file, just fetch it's last write time.
            if (File.Exists(path))
            {
                return new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
            }

            // If it's not a file and it's not a directory, it doesn't exist!
            if (!Directory.Exists(path))
            {
                return null;
            }

            // Start with the latest write time of the directory itself.
            var currentDate = new DateTimeOffset(Directory.GetLastWriteTimeUtc(path), TimeSpan.Zero);

            // Iterate through all the directories and files below
            // Recursively call GetLatestWriteDate to fetch their last write time.
            // Track the maximum value, and that's our answer.
            foreach (var entry in Directory.GetFileSystemEntries(path))
            {
                var entryDate = GetLatestWriteDate(entry);
                if (entryDate > currentDate)
                {
                    currentDate = entryDate.Value;
                }
            }
            return currentDate;
        }

        public static string CalculateMD5(string filename)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filename);

            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static void TeardownOverrideEventSystem(Scene? scene = null)
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            // should only be one of these existing at a time... but anyway
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Where(a=> scene == null || a.gameObject.scene == scene);
            foreach (var eventSystem in eventSystems)
            {
                if(eventSystem.gameObject.TryGetComponent<RGStandaloneInputModule>(out var rgModule))
                {
                    // remove it
                    Object.Destroy(rgModule);
                }

                List<BaseInputModule> inputModules = eventSystem.gameObject
                    .GetComponents<BaseInputModule>()
                    .Where(module => module is not RGStandaloneInputModule && module.inputOverride != null).ToList();

                // remove the override settings from the non RG one
                if (inputModules.Count > 0)
                {
                    foreach (var inputModule in inputModules)
                    {
                        // Reset and re-enable the one we overrode
                        inputModule.inputOverride = null;
                        inputModule.enabled = true;
                    }
                }

                if (eventSystem.gameObject.TryGetComponent<RGBaseInput>(out var biModule))
                {
                    Object.Destroy(biModule);
                }
            }
            #endif
        }

        /// <summary>
        /// Configures a scene's EventSystems to support replay and other functionality requiring simulated inputs
        /// </summary>
        public static void SetupOverrideEventSystem(Scene? scene = null)
        {
            // should only be one of these existing at a time... but anyway
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Where(a=> scene == null || a.gameObject.scene == scene);
            foreach (var eventSystem in eventSystems)
            {
                List<BaseInputModule> inputModules = eventSystem.gameObject
                    .GetComponents<BaseInputModule>()
                    #if ENABLE_LEGACY_INPUT_MANAGER
                    .Where(module => module is not RGStandaloneInputModule && module.isActiveAndEnabled).ToList();
                    #else
                    .ToList();
                    #endif

                // If there is no module, add the appropriate input module so that the replay can simulate UI inputs.
                // If both the new and old input systems are active, prefer the new input system's UI module.
                if (inputModules.Count == 0)
                {
                    #if ENABLE_INPUT_SYSTEM
                    inputModules = new List<BaseInputModule>() {eventSystem.gameObject.AddComponent<InputSystemUIInputModule>()};
                    #elif ENABLE_LEGACY_INPUT_MANAGER
                    inputModules = new List<BaseInputModule>() {eventSystem.gameObject.AddComponent<StandaloneInputModule>()};
                    #endif
                }

                #if ENABLE_LEGACY_INPUT_MANAGER
                foreach (var inputModule in inputModules)
                {
                    // Override the UI module's input source to read inputs from RGLegacyInputWrapper instead of UnityEngine.Input
                    if (inputModule != null && inputModule is not InputSystemUIInputModule && inputModule.inputOverride == null)
                    {
                        if (!eventSystem.gameObject.TryGetComponent<RGBaseInput>(out var rgBaseInput))
                        {
                            rgBaseInput = eventSystem.gameObject.AddComponent<RGBaseInput>();
                        }

                        // Override and disable the existing module's input
                        inputModule.inputOverride = rgBaseInput;
                        inputModule.enabled = false;

                        var rgModule = eventSystem.gameObject.GetComponent<RGStandaloneInputModule>();
                        if (rgModule == null)
                        {
                            // Add RGUIInputModule to read input from both playback and user input
                            eventSystem.gameObject.AddComponent<RGStandaloneInputModule>();
                        }
                    }
                }
#endif
            }
        }

        /// <summary>
        /// If any event systems don't have focus, then this forces a focused event
        /// for all MonoBehaviours in the scene.
        /// </summary>
        public static void ForceApplicationFocus()
        {
            bool anyNotFocused = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Any(eventSys => !eventSys.isFocused);
            if (anyNotFocused)
            {
                foreach (var behaviour in Object.FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
                {
                    behaviour.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
                }
            }
        }


        #if UNITY_EDITOR
        private static InputSettings.EditorInputBehaviorInPlayMode? _origEditorInputBehaviorInPlayMode;
        #endif
        private static InputSettings.BackgroundBehavior? _origBackgroundBehavior;

        /// <summary>
        /// Configures the input system settings for replay and bots.
        /// </summary>
        public static void ConfigureInputSettings()
        {
            #if ENABLE_INPUT_SYSTEM
            #if UNITY_EDITOR
            _origEditorInputBehaviorInPlayMode = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            #endif
            _origBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            #endif
        }

        /// <summary>
        /// Restores the input system settings to their previous values prior to the call to ConfigureInputSettings().
        /// </summary>
        public static void RestoreInputSettings()
        {
            #if UNITY_EDITOR
            if (_origEditorInputBehaviorInPlayMode.HasValue)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = _origEditorInputBehaviorInPlayMode.Value;
            }
            #endif
            if (_origBackgroundBehavior.HasValue)
            {
                InputSystem.settings.backgroundBehavior = _origBackgroundBehavior.Value;
            }
        }
    }
}
