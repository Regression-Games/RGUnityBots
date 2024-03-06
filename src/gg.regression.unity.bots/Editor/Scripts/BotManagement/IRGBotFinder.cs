using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class IRGBotFinder : Editor
{
    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        // Only run this method in the Unity editor, not at runtime
        if (!EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
        {
            return;
        }

        // Find the IRGBotList ScriptableObject in your project
        IRGBotList botList = LoadBotList();

        if (botList == null)
        {
            Debug.LogWarning("IRGBotList ScriptableObject not found");
            return;
        }

        // Use TypeCache to find all types that implement IRGBot
        var botTypes = TypeCache.GetTypesDerivedFrom<IRGBot>()
            .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) && !t.IsAbstract);

        List<IRGBotEntry> entries = new List<IRGBotEntry>();

        foreach (var type in botTypes)
        {
            // Find the MonoScript associated with each type
            var monoScripts = MonoImporter.GetAllRuntimeMonoScripts();
            var monoScript = monoScripts.FirstOrDefault(script => script.GetClass() == type);
            var qualifiedName = type.AssemblyQualifiedName;
            if (monoScript != null)
            {
                entries.Add(new IRGBotEntry
                {
                    botName = type.Name,
                    monoScript = monoScript,
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
        string packagePath = "Packages/gg.regression.unity.bots/Resources/RGBotList.asset";
        string localPath = "Assets/Runtime/Resources/RGBotList.asset";
        IRGBotList botList;
        botList = AssetDatabase.LoadAssetAtPath<IRGBotList>(packagePath);
        if (botList == null)
        {
            botList = AssetDatabase.LoadAssetAtPath<IRGBotList>(localPath);
        }

        return botList;
    }
}