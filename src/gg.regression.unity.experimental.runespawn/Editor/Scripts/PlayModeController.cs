using UnityEngine;
using UnityEditor;
using System;
using System.Net.Sockets;
using System.Collections.Generic;
using UnityEditor.Compilation;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames;


public static class PlayModeController
{
    private static bool originalRunInBackground = Application.runInBackground;

    private static List<string> _logs = new List<string>();
    private static List<string> _errors = new List<string>();

    private static BotSequence _botSequence;

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
    /// Starts Play mode and optionally runs a bot sequence.
    /// </summary>
    /// <param name="client">TcpClient object.</param>
    /// <param name="botSequencePath">Optional path to the bot sequence file.</param>
    public static void StartPlayMode(TcpClient client, string botSequencePath = null)
    {
        try
        {
            // Reset the previous bot sequence when a new request is received.
            _botSequence = null;
            SetupBotSequence(client, botSequencePath);

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
    /// Handles the loading and setup of a bot sequence.
    /// </summary>
    /// <param name="client">The TcpClient to send responses to.</param>
    /// <param name="botSequencePath">The path to the bot sequence file.</param>
    private static void SetupBotSequence(TcpClient client, string botSequencePath)
    {
        // If no bot sequence path is provided, return early.
        if (botSequencePath == null)
            return;

        // Load the bot sequence.
        _botSequence = BotSequence.LoadSequenceJsonFromPath(botSequencePath).Item3;

        // If the bot sequence is invalid, log an error, send an error response, and return.
        if (_botSequence == null)
        {
            // Ensure we don't have any duplicate subscriptions.
            EditorApplication.playModeStateChanged -= PlayBotSequenceOnPlayModeEnter;
            Debug.LogError($"Bot sequence file not found at path: {botSequencePath}");
            Utilities.SendJsonResponse(
                client.GetStream(),
                new { status = "Error", message = $"Bot sequence file not found at path: {botSequencePath}" }
            );
            return;
        }

        // If the bot sequence is valid, set up the play mode state change handler.
        EditorApplication.playModeStateChanged += PlayBotSequenceOnPlayModeEnter;
    }

    /// <summary>
    /// Handles the execution of a bot sequence when Unity enters Play Mode.
    /// </summary>
    /// <param name="state">The current state of the Play Mode transition.</param>
    private static void PlayBotSequenceOnPlayModeEnter(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Debug.Log($"PlayModeController: Playing bot sequence: {_botSequence.name}");
            // Unsubscribe to prevent multiple executions
            EditorApplication.playModeStateChanged -= PlayBotSequenceOnPlayModeEnter;
            _botSequence.Play();
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
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            _errors.Add($"[{type}] {logString}\n{stackTrace}");
        }
        else if (type == LogType.Log)
        {
            _logs.Add($"[{type}] {logString}"); // We don't need stack trace for normal logs.
        }
    }
}
