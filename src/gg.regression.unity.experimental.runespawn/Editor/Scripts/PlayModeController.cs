using UnityEngine;
using UnityEditor;
using System;
using System.Net.Sockets;
using UnityEditor.Compilation;


public static class PlayModeController
{
    private static bool originalRunInBackground = Application.runInBackground;

    /// <summary>
    /// Starts Play mode and sends a response to the client.
    /// </summary>
    /// <param name="client">TcpClient object.</param>
    public static void StartPlayMode(TcpClient client)
    {
        try
        {
            CompilationPipeline.RequestScriptCompilation();

            EditorApplication.delayCall += () =>
            {
                Application.runInBackground = true;
            };

            EditorApplication.isPlaying = true;
            Debug.Log("PlayModeController: Play mode started.");

            // Send success response
            Utilities.SendJsonResponse(client.GetStream(), new { status = "Started" });
        }
        catch (Exception ex)
        {
            Debug.LogError($"PlayModeController: Exception in StartPlayMode - {ex.Message}");
            Utilities.SendJsonResponse(client.GetStream(), new { status = "Error", message = ex.Message });
        }
        finally
        {
            client.Close();
        }
    }

    /// <summary>
    /// Stops Play mode and sends a response to the client.
    /// </summary>
    /// <param name="client">TcpClient object.</param>
    public static void StopPlayMode(TcpClient client)
    {
        try
        {
            EditorApplication.delayCall += () =>
            {
                Application.runInBackground = originalRunInBackground;
            };

            EditorApplication.isPlaying = false;
            Debug.Log("PlayModeController: Play mode stopped.");

            // Send success response
            Utilities.SendJsonResponse(client.GetStream(), new { status = "Stopped" });
        }
        catch (Exception ex)
        {
            Debug.LogError($"PlayModeController: Exception in StopPlayMode - {ex.Message}");
            Utilities.SendJsonResponse(client.GetStream(), new { status = "Error", message = ex.Message });
        }
        finally
        {
            client.Close();
        }
    }
}
