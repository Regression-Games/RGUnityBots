#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#endif
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace RegressionGames.Editor
{
#if UNITY_EDITOR
    
    /**
     * A class which copies over the SSL certs to our project
     */
    [InitializeOnLoad]
    public class SSLSetup : UnityEditor.Editor
    {
        static SSLSetup()
        {
            // Subscribe to the play mode state changed event
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Check if Unity has entered play mode
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                OnScriptsReloaded();
            }
        }

        /**
         * This verifies that the ssl cert is in the correct location and copies it over if it is not.
         */
        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            string sslCertPath = "Packages/gg.regression.unity.bots/Runtime/Resources/regression_cert.cer";
            // Load the file contents
            StreamReader reader = new StreamReader(sslCertPath); 
            string certContents = reader.ReadToEnd();
            reader.Close();

            // Ensure the target directory exists
            string targetDirPath = "Assets/RegressionGames/Resources";
            RGEditorUtils.CreateAllAssetFolders(targetDirPath);

            // Define the path for the new asset. We have to store this as a text file as Unity won't
            // load up cer files.
            string targetAssetPath = $"{targetDirPath}/regression_cert.txt";

            // Only copy over cert if it does not exist or it has changed
            var existingCert = AssetDatabase.LoadAssetAtPath<TextAsset>(targetAssetPath);
            if (existingCert == null || existingCert.text != certContents)
            {
                // Copy the asset
                AssetDatabase.CopyAsset(sslCertPath, targetAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            
            AssetDatabase.Refresh();
        }
    }
#endif
}
