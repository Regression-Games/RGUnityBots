using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RegressionGames
{
    public class RGUserSettings : ScriptableObject
    {
        [SerializeField] private string email;
        [SerializeField] private string password;

        private const string USER_SETTINGS_PATH = "Assets/UserSettings/RGUserSettings.asset";

        private static RGUserSettings _userSettings = null;
        private static bool dirty = true;

        public static RGUserSettings GetOrCreateUserSettings()
        {
            if (_userSettings == null || dirty)
            {
                try
                {
#if UNITY_EDITOR
                    _userSettings = AssetDatabase.LoadAssetAtPath<RGUserSettings>(USER_SETTINGS_PATH);
#endif
                    dirty = false;
                }
                catch (Exception)
                {
                    // if not called on main thread this will exception
                }
            }

            if (_userSettings == null)
            {
                _userSettings = CreateInstance<RGUserSettings>();
                _userSettings.email = "";
                _userSettings.password = "";
#if UNITY_EDITOR
                if (!Directory.Exists(USER_SETTINGS_PATH))
                {
                    Directory.CreateDirectory(USER_SETTINGS_PATH);
                }
                AssetDatabase.CreateAsset(_userSettings, USER_SETTINGS_PATH);
                AssetDatabase.SaveAssets();
#endif
            }

            return _userSettings;
        }

#if UNITY_EDITOR
        public static SerializedObject GetSerializedUserSettings()
        {
            return new SerializedObject(GetOrCreateUserSettings());
        }
#endif

        public static void OptionsUpdated()
        {
            //mark dirty
            dirty = true;
            try
            {
                // try to update and mark clean, but if failed
                // will keep trying to update until clean
#if UNITY_EDITOR
                _userSettings = AssetDatabase.LoadAssetAtPath<RGUserSettings>(USER_SETTINGS_PATH);
#endif
                dirty = false;
            }
            catch (Exception)
            {
                // if not called on main thread this will exception
            }
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
    }
}
