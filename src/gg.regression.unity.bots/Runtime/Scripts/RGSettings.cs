using System;
using UnityEditor;
using UnityEngine;

namespace RegressionGames
{
    public enum DebugLogLevel
    {
        Off,
        Verbose,
        Debug,
        Info,
        Warning,
        Error
    }

    public class RGSettings : ScriptableObject
    {
        private const string SETTINGS_PATH = "Assets/RGSettings.asset";

        [SerializeField] private bool useSystemSettings;
        [SerializeField] private bool enableOverlay;

        [SerializeField]
        [Obsolete("Feature no longer available, please start bots using the overlay")]
        private int numBots;

        [Obsolete("Feature no longer available, please start bots using the overlay")]
        [SerializeField]
        private long[] botsSelected;

        [SerializeField] private DebugLogLevel logLevel;
        [SerializeField] private string rgHostAddress;
        [SerializeField] private uint nextBotId;
#pragma warning disable CS0414 // suppress unused field warning
        [SerializeField] private uint nextBotInstanceId;
#pragma warning restore CS0414
        // ReSharper disable once InconsistentNaming
        [SerializeField] private bool feature_StateRecordingAndReplay;

        /*
         * This is setup to be safely callable on the non-main thread.
         * Options will update as soon as called on main thread once marked dirty.
         */
        private static RGSettings _settings;
        private static bool _dirty = true;

        public static RGSettings GetOrCreateSettings()
        {
            if (_settings == null || _dirty)
            {
                try
                {
#if UNITY_EDITOR
                    _settings = AssetDatabase.LoadAssetAtPath<RGSettings>(SETTINGS_PATH);
#endif
                    _dirty = false;
                }
                catch (Exception)
                {
                    // if not called on main thread this will exception
                }
            }

            if (_settings == null)
            {
                _settings = CreateInstance<RGSettings>();
                _settings.useSystemSettings = true;
                _settings.enableOverlay = true;
                _settings.rgHostAddress = "https://play.regression.gg";
                _settings.logLevel = DebugLogLevel.Info;

#pragma warning disable CS0618 // Type or member is obsolete
                _settings.numBots = 0;
                _settings.botsSelected = Array.Empty<long>();
#pragma warning restore CS0618 // Type or member is obsolete

                _settings.nextBotId = 0;
                _settings.nextBotInstanceId = 0;

                _settings.feature_StateRecordingAndReplay = false;
#if UNITY_EDITOR
                AssetDatabase.CreateAsset(_settings, SETTINGS_PATH);
                AssetDatabase.SaveAssets();
#endif
            }

            // These fields are now obsolete
#pragma warning disable CS0618 // Type or member is obsolete
            _settings.numBots = 0;
            _settings.botsSelected = Array.Empty<long>();
            _settings.nextBotId = 0;
            _settings.nextBotInstanceId = 0;
#pragma warning restore CS0618 // Type or member is obsolete

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
            _dirty = true;
            try
            {
                // try to update and mark clean, but if failed
                // will keep trying to update until clean
#if UNITY_EDITOR
                _settings = AssetDatabase.LoadAssetAtPath<RGSettings>(SETTINGS_PATH);
#endif
                _dirty = false;
            }
            catch (Exception)
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
#pragma warning disable CS0618 // Type or member is obsolete
            return numBots;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public long[] GetBotsSelected()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return botsSelected;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public DebugLogLevel GetLogLevel()
        {
            return logLevel;
        }

        public string GetRgHostAddress()
        {
            return rgHostAddress;
        }

        public bool GetFeatureStateRecordingAndReplay()
        {
            return feature_StateRecordingAndReplay;
        }
    }
}
