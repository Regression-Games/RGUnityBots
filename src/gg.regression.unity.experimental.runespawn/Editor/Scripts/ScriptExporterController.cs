using UnityEngine;
using UnityEditor;
using System;
using System.Net.Sockets;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public static class ScriptExporterController
{
    /// <summary>
    /// Retrieves and sends scripts to the client.
    /// </summary>
    /// <param name="client">TcpClient object.</param>
    public static void SendScriptsToClient(TcpClient client)
    {
        if (client == null)
        {
            Debug.LogError("ScriptExporterController: TcpClient is null for GetScripts command.");
            return;
        }

        try
        {
            // Load the ScriptExporterConfig asset
            string[] guids = AssetDatabase.FindAssets("t:ScriptExporterConfig");
            if (guids.Length == 0)
            {
                Debug.LogError("ScriptExporterController: No ScriptExporterConfig asset found. Please create one.");
                Utilities.SendJsonResponse(client.GetStream(), new { status = "Error", message = "No ScriptExporterConfig found." });
                client.Close();
                return;
            }

            if (guids.Length > 1)
            {
                Debug.LogError("ScriptExporterController: Multiple ScriptExporterConfig assets found. Please ensure only one exists.");
                Debug.Log("Found ScriptExporterConfig assets:");
                foreach (string guid in guids)
                {
                    Debug.Log(AssetDatabase.GUIDToAssetPath(guid));
                }
                Utilities.SendJsonResponse(client.GetStream(), new { status = "Error", message = "Multiple ScriptExporterConfig assets found." });
                client.Close();
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            ScriptExporterConfig config = AssetDatabase.LoadAssetAtPath<ScriptExporterConfig>(path);

            if (config == null)
            {
                Debug.LogError("ScriptExporterController: Failed to load ScriptExporterConfig.");
                Utilities.SendJsonResponse(client.GetStream(), new { status = "Error", message = "Failed to load ScriptExporterConfig." });
                client.Close();
                return;
            }

            // Get the JSON payload from the ScriptableObject
            string jsonPayload = config.GetSerializedScripts();

            if (string.IsNullOrEmpty(jsonPayload))
            {
                Debug.LogWarning("ScriptExporterController: No script data to send.");
                Utilities.SendJsonResponse(client.GetStream(), new { status = "Error", message = "No script data available." });
                client.Close();
                return;
            }

            // Deserialize the JSON payload to a dictionary
            var scriptsDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonPayload);

            // Send the JSON data to the client
            Utilities.SendJsonResponse(client.GetStream(), new { status = "Success", scripts = scriptsDict });

            client.Close();
        }
        catch (Exception ex)
        {
            Debug.LogError($"ScriptExporterController: Exception while sending scripts - {ex.Message}");
            Utilities.SendJsonResponse(client.GetStream(), new { status = "Error", message = ex.Message });
            client.Close();
        }
    }
}
