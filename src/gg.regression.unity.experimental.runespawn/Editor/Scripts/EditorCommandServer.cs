using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using RegressionGames;

[InitializeOnLoad]
public class EditorCommandServer
{
    // Enumeration for command types
    public enum CommandType
    {
        Start,
        Stop,
        GetScripts,
        Compile,
        GetCompilationMessages,
        GetRuntimeLogs
    }

    // Configuration: Read from environment variables with defaults
    private static readonly int Port = GetPort();
    private static readonly string AuthToken = GetAuthToken();

    private static Thread listenerThread;
    private static TcpListener listener;
    private static volatile bool stopThread = false;
    private static readonly Queue<EditorCommand> commandQueue = new Queue<EditorCommand>();

    static EditorCommandServer()
    {
        RGDebug.Log("EditorCommandServer: Starting");
        StartListening();
        EditorApplication.update += OnEditorUpdate;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        EditorApplication.quitting += OnEditorQuitting;

        // TODO (REG-2113): Use LoggingObserver to parse runtime logs.
        Application.logMessageReceived += PlayModeController.OnLogMessageReceived;
    }

    /// <summary>
    /// Retrieves the port number from environment variables or uses the default.
    /// </summary>
    /// <returns>Port number as integer.</returns>
    private static int GetPort()
    {
        string portEnv = Environment.GetEnvironmentVariable("UNITY_PORT");
        if (int.TryParse(portEnv, out int port))
        {
            return port;
        }
        return 9999; // Default port
    }

    /// <summary>
    /// Retrieves the authentication token from environment variables or uses the default.
    /// </summary>
    /// <returns>Authentication token as string.</returns>
    private static string GetAuthToken()
    {
        string token = Environment.GetEnvironmentVariable("UNITY_AUTH_TOKEN");
        return string.IsNullOrEmpty(token) ? "default_secret" : token;
    }

    /// <summary>
    /// Starts the listener thread to accept incoming TCP connections.
    /// </summary>
    private static void StartListening()
    {
        if (listenerThread != null && listenerThread.IsAlive)
        {
            return;
        }

        stopThread = false;

        listenerThread = new Thread(ListenForCommands)
        {
            IsBackground = true
        };
        listenerThread.Start();
        RGDebug.Log($"EditorCommandServer: Listening on port {Port}'");
    }

    /// <summary>
    /// Listens for incoming TCP connections and processes commands.
    /// </summary>
    private static void ListenForCommands()
    {
        listener = new TcpListener(IPAddress.Loopback, Port);
        listener.Start();
        RGDebug.Log("EditorCommandServer: TCP Listener started.");

        try
        {
            while (!stopThread)
            {
                if (listener.Pending())
                {
                    TcpClient client = listener.AcceptTcpClient();
                    NetworkStream stream = client.GetStream();
                    RGDebug.Log("EditorCommandServer: Client connected.");

                    // Handle client in a separate thread to prevent blocking
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                else
                {
                    Thread.Sleep(100); // Reduce CPU usage
                }
            }
        }
        catch (SocketException ex)
        {
            if (!stopThread)
            {
                RGDebug.LogError($"EditorCommandServer: SocketException in ListenForCommands: {ex.Message}");
            }
        }
        finally
        {
            listener.Stop();
            listener = null;
            RGDebug.Log("EditorCommandServer: TCP Listener stopped.");
        }
    }

    /// <summary>
    /// Handles individual client connections.
    /// </summary>
    /// <param name="obj">TcpClient object.</param>
    private static void HandleClient(object obj)
    {
        TcpClient client = obj as TcpClient;
        NetworkStream stream = client.GetStream();
        try
        {
            // Set a read timeout to prevent hanging
            stream.ReadTimeout = 5000; // 5 seconds

            // Read authentication token
            string receivedToken = Utilities.ReadLine(stream);
            if (receivedToken != AuthToken)
            {
                RGDebug.LogWarning("EditorCommandServer: Authentication failed.");
                Utilities.SendJsonResponse(stream, new { status = "Error", message = "Authentication Failed" });
                client.Close();
                return;
            }

            // Send authentication success
            Utilities.SendJsonResponse(stream, new { status = "OK" });

            // Read the command
            string commandStr = Utilities.ReadLine(stream);
            RGDebug.Log($"EditorCommandServer: Received command '{commandStr}'");

            if (Enum.TryParse(commandStr, true, out CommandType commandType))
            {
                EditorCommand editorCommand = new EditorCommand(commandType, client);
                lock (commandQueue)
                {
                    commandQueue.Enqueue(editorCommand);
                }
            }
            else
            {
                RGDebug.LogWarning($"EditorCommandServer: Unknown command received - '{commandStr}'");
                Utilities.SendJsonResponse(stream, new { status = "Error", message = $"Unknown command '{commandStr}'" });
                client.Close();
            }
        }
        catch (IOException ex)
        {
            RGDebug.LogError($"EditorCommandServer: IOException while handling client: {ex.Message}");
            client.Close();
        }
        catch (Exception ex)
        {
            RGDebug.LogError($"EditorCommandServer: Exception while handling client: {ex.Message}");
            client.Close();
        }
    }

    /// <summary>
    /// Called on each editor update to process queued commands.
    /// </summary>
    private static void OnEditorUpdate()
    {
        lock (commandQueue)
        {
            while (commandQueue.Count > 0)
            {
                EditorCommand editorCommand = commandQueue.Dequeue();
                RGDebug.Log("EditorCommandServer: Processing command - " + editorCommand.CommandType);

                switch (editorCommand.CommandType)
                {
                    case CommandType.Start:
                        PlayModeController.StartPlayMode(editorCommand.Client);
                        break;

                    case CommandType.Stop:
                        PlayModeController.StopPlayMode(editorCommand.Client);
                        break;

                    case CommandType.GetRuntimeLogs:
                        PlayModeController.SendRuntimeLogsToClient(editorCommand.Client);
                        break;

                    case CommandType.GetScripts:
                        ScriptExporterController.SendScriptsToClient(editorCommand.Client);
                        break;

                    case CommandType.GetCompilationMessages:
                        CompilationController.SendCompilationErrorsToClient(editorCommand.Client);
                        break;

                    case CommandType.Compile:
                        CompilationController.CompileProject(editorCommand.Client);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Stops the listener thread and cleans up resources.
    /// </summary>
    private static void StopListening()
    {
        if (listenerThread != null && listenerThread.IsAlive)
        {
            stopThread = true;

            try
            {
                listener.Stop();
            }
            catch (Exception ex)
            {
                RGDebug.LogError($"EditorCommandServer: Exception while stopping listener: {ex.Message}");
            }

            listenerThread.Join();
            listenerThread = null;
            listener = null;
        }

        // Close any stored client connections
        CompilationController.Cleanup();

        // Unsubscribe from events
        EditorApplication.update -= OnEditorUpdate;
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        EditorApplication.quitting -= OnEditorQuitting;
        Application.logMessageReceived -= PlayModeController.OnLogMessageReceived;
    }

    /// <summary>
    /// Event handler called before assembly reload.
    /// </summary>
    private static void OnBeforeAssemblyReload()
    {
        StopListening();
    }

    /// <summary>
    /// Event handler called when the Unity Editor is quitting.
    /// </summary>
    private static void OnEditorQuitting()
    {
        StopListening();
    }
}

/// <summary>
/// Represents a command received from the client.
/// </summary>
public class EditorCommand
{
    public EditorCommandServer.CommandType CommandType { get; private set; }
    public TcpClient Client { get; private set; }

    public EditorCommand(EditorCommandServer.CommandType commandType, TcpClient client)
    {
        CommandType = commandType;
        Client = client;
    }
}
