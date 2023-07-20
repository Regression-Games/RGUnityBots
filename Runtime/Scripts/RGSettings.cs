using System;
using UnityEditor;
using UnityEngine;

namespace RegressionGames
{
    public enum DebugLogLevel {Off, Verbose, Debug, Info, Warning, Error}
    
    public class RGSettings: ScriptableObject
    {
        public const string SETTINGS_PATH = "Assets/RGSettings.asset";

        [SerializeField] private bool useSystemSettings;
        [SerializeField] private bool enableOverlay;
        [SerializeField] private int numBots;
        [SerializeField] private string email;
        [SerializeField] private string password;
        [SerializeField] private int[] botsSelected;
        [SerializeField] private DebugLogLevel logLevel;

        /*
         * This is setup to be safely callable on the non-main thread.
         * Options will update as soon as called on main thread once marked dirty.
         */
        private static RGSettings _settings = null;
        private static bool dirty = true;
        
        public static RGSettings GetOrCreateSettings()
        {
            if (_settings == null || dirty)
            {
                try
                {
                    _settings = AssetDatabase.LoadAssetAtPath<RGSettings>(SETTINGS_PATH);
                    dirty = false;
                }
                catch (Exception ex)
                {
                    // if not called on main thread this will exception
                }
            }

            if (_settings == null)
            {
                _settings = CreateInstance<RGSettings>();
                _settings.useSystemSettings = false;
                _settings.enableOverlay = true;
                _settings.numBots = 0;
                _settings.email = "rgunitydev@rgunity.com";
                _settings.password = "Password1";
                _settings.botsSelected = new int[0];
                AssetDatabase.CreateAsset(_settings, SETTINGS_PATH);
                AssetDatabase.SaveAssets();
            }
            
            return _settings;
        }

        public static void OptionsUpdated()
        {
            //mark dirty
            dirty = true;
            try
            {
                // try to update and mark clean, but if failed
                // will keep trying to update until clean
                _settings = AssetDatabase.LoadAssetAtPath<RGSettings>(SETTINGS_PATH);
                dirty = false;
            }
            catch (Exception ex)
            {
                // if not called on main thread this will exception
            }
        }

        public static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }

        public bool GetUseSystemSettings()
        {
            return useSystemSettings;
        }

        public bool GetEnableOverlay()
        {
            return enableOverlay;
        }

        public int GetNumBots()
        {
            return numBots;
        }

        public string GetEmail()
        {
            return email;
        }

        public string GetPassword()
        {
            return password;
        }

        public int[] GetBotsSelected()
        {
            return botsSelected;
        }

        public DebugLogLevel GetLogLevel()
        {
            return logLevel;
        }
    }
    
}
