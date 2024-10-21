using UnityEngine;
using UnityEditor;
using System;
using System.Net.Sockets;
using System.Collections.Generic;
using UnityEditor.Compilation;
using RegressionGames;


public static class PlayModeController
{
    private static bool originalRunInBackground = Application.runInBackground;

    private static List<string> _logs = new List<string>();
    private static List<string> _errors = new List<string>();

    /// <summary>
    /// Static constructor for PlayModeController.
    /// Initializes event subscriptions and prevents multiple subscriptions.
    /// </summary>
    static PlayModeController()
    {
        // Prevent multiple subscriptions.
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= CleanUpSubscriptions;

        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        // Unsubscribe from playModeStateChanged before the assembly reloads.
        // This is to prevent multiple subscriptions.
        AssemblyReloadEvents.beforeAssemblyReload += CleanUpSubscriptions;
    }

    /// <summary>
    /// Cleans up event subscriptions to prevent multiple subscriptions.
    /// This is called before the assembly is reloaded.
    /// </summary>
    private static void CleanUpSubscriptions()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= CleanUpSubscriptions;
    }

    /// <summary>
    /// Clears logs and errors when entering play mode.
    /// </summary>
    /// <param name="state">The current state of the play mode.</param>
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // Clear logs and errors when manually entering play mode as well. 
            _logs.Clear();
            _errors.Clear();
        }
    }

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
            RGDebug.Log("PlayModeController: Play mode started.");

            // Clear the previous logs and errors when entering play mode.
            _logs.Clear();
            _errors.Clear();
           
            // Send success response
            Utilities.SendJsonResponse(client.GetStream(), new { status = "Started" });
        }
        catch (Exception ex)
        {
            RGDebug.LogError($"PlayModeController: Exception in StartPlayMode - {ex.Message}");
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
            RGDebug.Log("PlayModeController: Play mode stopped.");

            // Send success response
            Utilities.SendJsonResponse(client.GetStream(), new { status = "Stopped" });
        }
        catch (Exception ex)
        {
            RGDebug.LogError($"PlayModeController: Exception in StopPlayMode - {ex.Message}");
            Utilities.SendJsonResponse(client.GetStream(), new { status = "Error", message = ex.Message });
        }
        finally
        {
            client.Close();
        }
    }

    /// <summary>
    /// Sends the collected runtime logs and errors to the connected client.
    /// </summary>
    /// <param name="client">The TcpClient object representing the connected client.</param>
    public static void SendRuntimeLogsToClient(TcpClient client)
    {
        try
        {
            string logs_string = string.Join("\n", _logs);
            string errors_string = string.Join("\n", _errors);
            Utilities.SendJsonResponse(client.GetStream(), new {
                status = "Success",
                logs = logs_string,
                errors = errors_string
            });
        }
        catch (Exception ex)
        {
            RGDebug.LogError($"PlayModeController: Exception in SendLogsToClient - {ex.Message}");
            Utilities.SendJsonResponse(client.GetStream(), new { status = "Error", message = ex.Message });
        }
        finally
        {
            client.Close();
        }
    }


    /// <summary>
    /// Handles log messages received from Unity.
    /// </summary>
    /// <param name="logString">The log message.</param>
    /// <param name="stackTrace">The stack trace (if any).</param>
    /// <param name="type">The type of log message.</param>
    public static void OnLogMessageReceived(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning || type == LogType.Assert)
        {
            _errors.Add($"[{type}] {logString}\n{stackTrace}\n");
        }
        else if (type == LogType.Log)
        {
            _logs.Add($"[{type}] {logString}"); // We don't need stack trace for normal logs.
        }
    }
}
