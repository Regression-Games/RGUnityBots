using UnityEditor;
using UnityEngine;

namespace RegressionGames
{
    public class RGSettings: ScriptableObject
    {
        public const string SETTINGS_PATH = "Assets/RGSettings.asset";

        [SerializeField] private bool useSystemSettings;
        [SerializeField] private bool enableOverlay;
        [SerializeField] private int numBots;
        [SerializeField] private string username;
        [SerializeField] private string password;
        [SerializeField] private int[] botsSelected;
        
        public static RGSettings GetOrCreateSettings()
        {
            RGSettings settings = AssetDatabase.LoadAssetAtPath<RGSettings>(SETTINGS_PATH);
            if (settings == null)
            {
                settings = CreateInstance<RGSettings>();
                settings.useSystemSettings = false;
                settings.enableOverlay = true;
                settings.numBots = 0;
                settings.username = "rgunitydev@rgunity.com";
                settings.password = "Password1";
                settings.botsSelected = new int[0];
                AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
                AssetDatabase.SaveAssets();
            }
            return settings;
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

        public string GetUsername()
        {
            return username;
        }

        public string GetPassword()
        {
            return password;
        }

        public int[] GetBotsSelected()
        {
            return botsSelected;
        }

    }
    
}
