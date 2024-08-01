#if ENABLE_LEGACY_INPUT_MANAGER
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

        public static string GetInputManagerSettingsJSON()
        {
            var inputManagerAsset = AssetDatabase.LoadAllAssetsAtPath(_inputManagerAssetPath).FirstOrDefault();
            if (inputManagerAsset != null)
            {
                return EditorJsonUtility.ToJson(inputManagerAsset);
            }
            else
            {
                return null;
            }
        }
    }
}
#endif
#endif