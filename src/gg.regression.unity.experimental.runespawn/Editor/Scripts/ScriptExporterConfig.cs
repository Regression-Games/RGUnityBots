using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

[CreateAssetMenu(fileName = "ScriptExporterConfig", menuName = "Script Exporter/Config", order = 1)]
public class ScriptExporterConfig : ScriptableObject
{
    [Header("Scripts to Export")]
    [Tooltip("Add the scripts you want to send to the external LLM.")]
    public List<MonoScript> scriptsToExport;

    // Method to get the serialized JSON payload
    public string GetSerializedScripts()
    {
        Dictionary<string, string> scriptContents = new Dictionary<string, string>();

        foreach (MonoScript monoScript in scriptsToExport)
        {
            if (monoScript == null)
                continue;

            string scriptPath = AssetDatabase.GetAssetPath(monoScript);
            string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), scriptPath);

            if (File.Exists(fullPath))
            {
                string content = File.ReadAllText(fullPath);
                scriptContents.Add(monoScript.name, content);
            }
            else
            {
                Debug.LogWarning($"ScriptExporterConfig: Script file not found at path: {fullPath}");
            }
        }

        if (scriptContents.Count == 0)
        {
            Debug.LogWarning("ScriptExporterConfig: No scripts found to serialize.");
            return null;
        }

        // Serialize the dictionary to JSON using Newtonsoft.Json
        string json = JsonConvert.SerializeObject(scriptContents, Formatting.Indented);
        return json;
    }
}
