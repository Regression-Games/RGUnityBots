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
     * as a JSON file prior to a standalone build starting.
     */
    public class RGLegacyInputSettingsHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        
        public void OnPreprocessBuild(BuildReport report)
        {
            RGLegacyEditorOnlyUtils.WriteInputManagerSettingsCopy();
        }
    }
}

#endif