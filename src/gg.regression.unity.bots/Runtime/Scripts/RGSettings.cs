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
        [SerializeField] private long[] botsSelected;
        [SerializeField] private DebugLogLevel logLevel;
        [SerializeField] private string rgHostAddress;
        [SerializeField] private uint nextBotId;
        [SerializeField] private uint nextBotInstanceId;

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
#if UNITY_EDITOR
                    _settings = AssetDatabase.LoadAssetAtPath<RGSettings>(SETTINGS_PATH);
#endif
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
                _settings.useSystemSettings = true;
                _settings.enableOverlay = true;
                _settings.numBots = 0;
                _settings.email = "";
                _settings.password = "";
                _settings.botsSelected = Array.Empty<long>();
                _settings.rgHostAddress = "https://play.regression.gg";
                _settings.logLevel = DebugLogLevel.Info;
                _settings.nextBotId = 0;
                _settings.nextBotInstanceId = 0;
#if UNITY_EDITOR
                AssetDatabase.CreateAsset(_settings, SETTINGS_PATH);
                AssetDatabase.SaveAssets();
#endif
            }
            
            // backwards compat for migrating RG devs before we had a single host address field
            if (string.IsNullOrEmpty(_settings.rgHostAddress))
            {
                _settings.rgHostAddress = "https://play.regression.gg";
#if UNITY_EDITOR
                AssetDatabase.SaveAssets();
#endif
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
#if UNITY_EDITOR
                _settings = AssetDatabase.LoadAssetAtPath<RGSettings>(SETTINGS_PATH);
#endif
                dirty = false;
            }
            catch (Exception ex)
            {
                // if not called on main thread this will exception
            }
        }

#if UNITY_EDITOR
        public static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
#endif

        public static long GetSystemId()
        {
            // a machine unique system id shifted into the left side of the long as a human would see it
            return SystemInfo.deviceUniqueIdentifier.GetHashCode() * 1_000_000_000L;
        }

        public long GetNextBotId()
        {
            var systemId = GetSystemId();
            var nextId = nextBotId++;
#if UNITY_EDITOR
            AssetDatabase.SaveAssets();
#endif
            // this is so that 'to a human' these ids look sequential
            if (systemId < 0)
            {
                systemId -= nextId;
            }
            else
            {
                systemId += nextId;
            }
            return systemId;
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

        public void SetEmail(string newEmail)
        {
            email = newEmail;
        }
        
        public string GetPassword()
        {
            return password;
        }

        public void SetPassword(string newPassword)
        {
            password = newPassword;
        }
        
        public long[] GetBotsSelected()
        {
            return botsSelected;
        }

        public DebugLogLevel GetLogLevel()
        {
            return logLevel;
        }

        public string GetRgHostAddress()
        {
            return rgHostAddress;
        }
    }
    
}
