#if UNITY_EDITOR
using System.IO;
using System.Linq;
using RegressionGames.RGLegacyInputUtility;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace RegressionGames.Editor.RGLegacyInputUtility
{
    /**
     * This script is responsible for making a copy of the input manager settings
     * as a JSON asset prior to a standalone build starting.
     */
    public class RGLegacyInputSettingsPreprocessBuild : IPreprocessBuildWithReport
    {
        private static string _inputManagerJsonOut = "Assets/Resources/RGInputSettingsCopy.json";
        public int callbackOrder => 0;
        
        /*
         * Creates a copy of the input manager settings as a JSON file so that
         * our tools can read them in the standalone build during game play.
         */
        public void OnPreprocessBuild(BuildReport report)
        {
            string json = RGLegacyEditorOnlyUtils.GetInputManagerSettingsJSON();
            if (json != null)
            {
                if (File.Exists(_inputManagerJsonOut))
                {
                    File.Delete(_inputManagerJsonOut);
                }
                File.WriteAllText(_inputManagerJsonOut, json);
            }
        }
    }
}

#endif