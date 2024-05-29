#if UNITY_EDITOR
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace RegressionGames.RGLegacyInputUtility
{
    public class RGLegacyEditorOnlyUtils
    {
        private static string _inputManagerAssetPath = "ProjectSettings/InputManager.asset";
        private static string _inputManagerJsonOut = "Assets/Resources/RGInputSettingsCopy.json";
        
        /*
         * Creates a copy of the input manager settings as a JSON file so that
         * our tools can read them during game play.
         */
        public static void WriteInputManagerSettingsCopy()
        {
            var inputManagerAsset = AssetDatabase.LoadAllAssetsAtPath(_inputManagerAssetPath).FirstOrDefault();
            if (inputManagerAsset != null)
            {
                string json = EditorJsonUtility.ToJson(inputManagerAsset);
                if (File.Exists(_inputManagerJsonOut))
                {
                    File.Delete(json);
                }
                File.WriteAllText(_inputManagerJsonOut, json);
            }
        }
    }
}
#endif