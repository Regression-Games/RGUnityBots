#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.RGLegacyInputUtility
{
    public enum InputManagerEntryType
    {
        KEY_OR_MOUSE_BUTTON = 0,
        MOUSE_MOVEMENT = 1,
        JOYSTICK_AXIS = 2
    }
    
    // The following structs' field names must match those found in the asset files.
    [Serializable]
    public class InputManagerEntry
    {
        public string m_Name;
        public string descriptiveName;
        public string descriptiveNegativeName;
        public string negativeButton;
        public string positiveButton;
        public string altNegativeButton;
        public string altPositiveButton;
        public float gravity;
        public float dead;
        public float sensitivity;
        public bool snap;
        public bool invert;
        public InputManagerEntryType type;
        public int axis;
        public int joyNum;
        
        // The following fields are added afterward (not part of the original data)
        public KeyCode? negativeButtonKeyCode;
        public KeyCode? positiveButtonKeyCode;
        public KeyCode? altNegativeButtonKeyCode;
        public KeyCode? altPositiveButtonKeyCode;
    }

    [Serializable]
    class InputManagerSettingsData
    {
        public InputManagerEntry[] m_Axes;
    }

    [Serializable]
    class InputManagerSettingsRoot
    {
        public InputManagerSettingsData InputManager;
    }
    
    public class RGLegacyInputManagerSettings
    {
        private InputManagerEntry[] _entries;
        private Dictionary<string, List<InputManagerEntry>> _entriesByName;
        
        public IEnumerable<InputManagerEntry> Entries => _entries;
        
        public RGLegacyInputManagerSettings()
        {
            // If we're running the game within the editor, then make a fresh copy of the 
            // input manager settings JSON in case anything changed. Otherwise,
            // use the existing JSON file that would have been created by RGLegacyInputSettingsHook.
            #if UNITY_EDITOR
            string json = RGLegacyEditorOnlyUtils.GetInputManagerSettingsJSON();
            #else
            TextAsset jsonFile = Resources.Load<TextAsset>("RGInputSettingsCopy");
            string json = jsonFile?.text;
            #endif

            if (json != null)
            {
                InputManagerSettingsRoot root = JsonUtility.FromJson<InputManagerSettingsRoot>(json);
                _entries = root.InputManager.m_Axes;
            }
            else
            {
                _entries = Array.Empty<InputManagerEntry>();
                RGDebug.LogWarning("Missing RGInputSettingsCopy.json, simulating some legacy inputs will not work");
            }
            
            _entriesByName = new Dictionary<string, List<InputManagerEntry>>();
            foreach (var entry in _entries)
            {
                if (!string.IsNullOrEmpty(entry.negativeButton))
                {
                    entry.negativeButtonKeyCode = RGLegacyInputWrapper.KeyNameToCode(entry.negativeButton);
                }
                if (!string.IsNullOrEmpty(entry.positiveButton))
                {
                    entry.positiveButtonKeyCode = RGLegacyInputWrapper.KeyNameToCode(entry.positiveButton);
                }
                if (!string.IsNullOrEmpty(entry.altNegativeButton))
                {
                    entry.altNegativeButtonKeyCode = RGLegacyInputWrapper.KeyNameToCode(entry.altNegativeButton);
                }
                if (!string.IsNullOrEmpty(entry.altPositiveButton))
                {
                    entry.altPositiveButtonKeyCode = RGLegacyInputWrapper.KeyNameToCode(entry.altPositiveButton);
                }

                if (_entriesByName.TryGetValue(entry.m_Name, out var entriesWithName))
                {
                    entriesWithName.Add(entry);
                }
                else
                {
                    _entriesByName.Add(entry.m_Name, new List<InputManagerEntry> {entry});
                }
            }
        }
        
        public IEnumerable<InputManagerEntry> GetEntriesByName(string name)
        {
            if (_entriesByName.TryGetValue(name, out var entriesWithName))
            {
                foreach (var entry in entriesWithName)
                {
                    yield return entry;
                }
            }
        }
    }
}
#endif