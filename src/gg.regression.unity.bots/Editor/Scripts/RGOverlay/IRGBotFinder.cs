using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace RegressionGames.Editor
{
    [InitializeOnLoad]
    public class IRGBotFinder : UnityEditor.Editor
    {
        static IRGBotFinder()
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

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            // Find the IRGBotList ScriptableObject in your project
            IRGBotList botList = LoadBotList();

            if (botList == null)
            {
                return;
            }

            // Use TypeCache to find all types that implement IRGBot
            var botTypes = TypeCache.GetTypesDerivedFrom<IRGBot>()
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) && !t.IsAbstract);

            List<IRGBotEntry> entries = new List<IRGBotEntry>();

            foreach (var type in botTypes)
            {
                var qualifiedName = type.AssemblyQualifiedName;
                if (!string.IsNullOrEmpty(qualifiedName))
                {
                    entries.Add(new IRGBotEntry
                    {
                        botName = type.Name,
                        qualifiedName = qualifiedName,
                    });
                }
            }

            botList.botEntries = entries.ToArray();

            // Mark the ScriptableObject as dirty to ensure it saves
            EditorUtility.SetDirty(botList);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static IRGBotList LoadBotList()
        {
            string packagePath = "Packages/gg.regression.unity.bots/Runtime/Resources/RGBotList.asset";
            IRGBotList botList = AssetDatabase.LoadAssetAtPath<IRGBotList>(packagePath);

            // Ensure the target SO directory exists
            string targetDirPath = "Assets/RegressionGames/Resources";
            RGEditorUtils.CreateAllAssetFolders(targetDirPath);

            // Define the path for the new asset
            string targetAssetPath = $"{targetDirPath}/RGBots.asset";

            // Check if the asset already exists to avoid overwriting it
            if (AssetDatabase.LoadAssetAtPath<IRGBotList>(targetAssetPath) == null)
            {
                // Copy the asset
                AssetDatabase.CopyAsset(packagePath, targetAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            botList = AssetDatabase.LoadAssetAtPath<IRGBotList>(targetAssetPath);
            AssetDatabase.Refresh();
            return botList;
        }
    }
}