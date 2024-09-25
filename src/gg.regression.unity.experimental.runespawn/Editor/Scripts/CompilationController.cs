using UnityEngine;
using UnityEditor;
using System;
using System.Net.Sockets;
using System.Collections.Generic;
using UnityEditor.Compilation;
using System.Threading;
using Newtonsoft.Json;

public static class CompilationController
{
    private static List<CompilationMessage> compilationErrors = new List<CompilationMessage>();
    private static readonly object errorLock = new object();
    private static TcpClient compileClient;

    static CompilationController()
    {
        // Subscribe to compilation finished events
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
        CompilationPipeline.compilationFinished += OnCompilationFinished;
    }

    /// <summary>
    /// Initiates project compilation and stores the client to respond later.
    /// </summary>
    /// <param name="client">TcpClient object.</param>
    public static void CompileProject(TcpClient client)
    {
        if (client == null)
        {
            Debug.LogError("CompilationController: TcpClient is null for Compile command.");
            return;
        }

        // Store the client to respond later after compilation
        compileClient = client;

        // Request script compilation
        CompilationPipeline.RequestScriptCompilation();
    }

    /// <summary>
    /// Retrieves and sends compilation errors to the client.
    /// </summary>
    /// <param name="client">TcpClient object.</param>
    public static void SendCompilationErrorsToClient(TcpClient client)
    {
        if (client == null)
        {
            Debug.LogError("CompilationController: TcpClient is null for GetCompilationMessages command.");
            return;
        }

        try
        {
            List<CompilationMessage> messageCopy;

            // Lock the error list to prevent race conditions
            lock (errorLock)
            {
                messageCopy = new List<CompilationMessage>(compilationErrors);
            }

            if (messageCopy.Count == 0)
            {
                // If no errors, send a success message
                messageCopy.Add(new CompilationMessage
                {
                    File = "",
                    Line = 0,
                    Column = 0,
                    Message = "Compilation finished successfully.",
                    Type = "Success"
                });
            }

            // Send the JSON data to the client
            Utilities.SendJsonResponse(client.GetStream(), new { status = "Success", messages = messageCopy });

            client.Close();
        }
        catch (Exception ex)
        {
            Debug.LogError($"CompilationController: Exception while sending compilation errors - {ex.Message}");
            Utilities.SendJsonResponse(client.GetStream(), new { status = "Error", message = ex.Message });
            client.Close();
        }
    }

    /// <summary>
    /// Event handler for when an assembly compilation is finished.
    /// Captures compilation errors.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly.</param>
    /// <param name="messages">Compilation messages.</param>
    private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
        lock (errorLock)
        {
            compilationErrors.Clear();

            foreach (var message in messages)
            {
                CompilationMessage compMessage = new CompilationMessage
                {
                    File = message.file,
                    Line = message.line,
                    Column = message.column,
                    Message = message.message,
                    Type = message.type.ToString()
                };
                compilationErrors.Add(compMessage);
            }
        }
    }

    /// <summary>
    /// Event handler for when compilation is finished.
    /// Sends a response to the compile client.
    /// </summary>
    /// <param name="context">Compilation context.</param>
    private static void OnCompilationFinished(object context)
    {
        Debug.Log("CompilationController: Compilation finished.");
        if (compileClient != null)
        {
            SendCompileResponse();
        }
    }

    /// <summary>
    /// Sends the compilation response to the compile client.
    /// </summary>
    private static void SendCompileResponse()
    {
        try
        {
            Debug.Log("CompilationController: Sending compile response.");
            // Send the response to the client
            Utilities.SendJsonResponse(compileClient.GetStream(), new { status = "Complete", message = "Complete" });

            // Close the connection
            compileClient.Close();
            compileClient = null; // Reset the stored client
        }
        catch (Exception ex)
        {
            Debug.LogError($"CompilationController: Exception while sending compile response - {ex.Message}");
            if (compileClient != null)
            {
                Utilities.SendJsonResponse(compileClient.GetStream(), new { status = "Error", message = ex.Message });
                compileClient.Close();
                compileClient = null;
            }
        }
    }

    /// <summary>
    /// Cleans up resources and unsubscribes from events.
    /// </summary>
    public static void Cleanup()
    {
        // Close any stored client connections
        if (compileClient != null)
        {
            Debug.Log("CompilationController: Closing compileClient.");
            compileClient.Close();
            compileClient = null;
        }

        // Unsubscribe from compilation events
        CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
        CompilationPipeline.compilationFinished -= OnCompilationFinished;
    }
}

/// <summary>
/// Represents a compilation message.
/// </summary>
public class CompilationMessage
{
    public string File { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; }
    public string Type { get; set; }
}
