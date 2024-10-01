using System;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

public static class Utilities
{
    /// <summary>
    /// Reads a line terminated by newline from the network stream.
    /// </summary>
    /// <param name="stream">NetworkStream object.</param>
    /// <returns>String read from the stream.</returns>
    public static string ReadLine(NetworkStream stream)
    {
        StringBuilder sb = new StringBuilder();
        int data;
        while ((data = stream.ReadByte()) != -1)
        {
            if (data == '\n')
                break;
            sb.Append((char)data);
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Sends a JSON response to the client.
    /// </summary>
    /// <param name="stream">NetworkStream object.</param>
    /// <param name="response">Object representing the response.</param>
    public static void SendJsonResponse(NetworkStream stream, object response)
    {
        string json = JsonConvert.SerializeObject(response);
        byte[] data = Encoding.UTF8.GetBytes(json + "\n");
        stream.Write(data, 0, data.Length);
        stream.Flush();
    }
}
