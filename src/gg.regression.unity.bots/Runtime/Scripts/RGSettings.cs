using System;
using System.IO;
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
        // We store this asset in the Resources folder so that it can be accessed from any build
        // Note that it can only be created in the editor, however.
        private static readonly string SETTINGS_DIRECTORY = "Assets/Resources";
        private static readonly string SETTINGS_RESOURCE_NAME = "RGSettings";
        private static readonly string SETTINGS_PATH = $"{SETTINGS_DIRECTORY}/{SETTINGS_RESOURCE_NAME}.asset";

        // General settings about how the SDK should operate
        [SerializeField] private bool useSystemSettings;
        [SerializeField] private DebugLogLevel logLevel;
        // ReSharper disable once InconsistentNaming
        [SerializeField] private bool feature_StateRecordingAndReplay;
        // ReSharper disable once InconsistentNaming
        [SerializeField] private bool feature_CodeCoverage;

        // default these to FALSE to avoid ECS performance overhead out of the box
        // ReSharper disable once InconsistentNaming
        [SerializeField] private bool feature_CaptureEntityState;
        // ReSharper disable once InconsistentNaming
        [SerializeField] private bool feature_CaptureEntityComponentData;

        // Authentication settings
        [SerializeField] private string rgHostAddress;
        [SerializeField] private string apiKey;

        // AI Service Settings - Use "http://127.0.0.1:18888/" for local aiservice dev testing
        // NOTE: By default this is left as NULL and tracks rgHostAddress value unless overridden
        [SerializeField] private string aiServiceHostAddress;

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
                    _settings = Resources.Load<RGSettings>(SETTINGS_RESOURCE_NAME);
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
                _settings.rgHostAddress = "https://play.regression.gg";
                _settings.logLevel = DebugLogLevel.Info;

                _settings.feature_StateRecordingAndReplay = true;
                _settings.feature_CodeCoverage = false;
                _settings.feature_CaptureEntityState = true;
                _settings.feature_CaptureEntityComponentData = false;
#if UNITY_EDITOR
                Directory.CreateDirectory(SETTINGS_DIRECTORY);
                AssetDatabase.CreateAsset(_settings, SETTINGS_PATH);
                AssetDatabase.SaveAssets();
#endif
#if !UNITY_EDITOR
                Debug.LogWarning("RG settings could not be loaded. Make sure to log into Regression Games within the Unity Editor before building your project. For now, an empty user settings object will be used.");
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
                _settings = Resources.Load<RGSettings>(SETTINGS_RESOURCE_NAME);
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

        public bool GetUseSystemSettings()
        {
            return useSystemSettings;
        }

        public DebugLogLevel GetLogLevel()
        {
            return logLevel;
        }

        public string GetApiKey()
        {
            return apiKey;
        }

        public string GetRgHostAddress()
        {
            return rgHostAddress;
        }

        public string GetAIServiceHostAddress()
        {
            // Tracks 'rgHostAddress' unless explicitly overridden
            if (string.IsNullOrEmpty(aiServiceHostAddress))
            {
                return rgHostAddress;
            }
            return aiServiceHostAddress;
        }

        public bool GetFeatureStateRecordingAndReplay()
        {
            return feature_StateRecordingAndReplay;
        }

        public bool GetFeatureCodeCoverage()
        {
            return feature_CodeCoverage;
        }

        public bool GetFeatureCaptureEntityState()
        {
            return feature_CaptureEntityState;
        }

        public bool GetFeatureCaptureEntityComponentData()
        {
            return feature_CaptureEntityComponentData;
        }
    }
}
