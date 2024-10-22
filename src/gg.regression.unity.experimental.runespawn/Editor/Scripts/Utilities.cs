using System;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

public static class Utilities
{
    /// <summary>
    /// Reads a line terminated by newline from the network stream with timeout.
    /// </summary>
    /// <param name="stream">NetworkStream object.</param>
    /// <param name="timeoutMilliseconds">Timeout in milliseconds. Default is 5000 (5 seconds).</param>
    /// <returns>String read from the stream.</returns>
    /// <exception cref="TimeoutException">Thrown when the read operation times out.</exception>
    public static string ReadLine(NetworkStream stream, int timeoutMilliseconds = 5000)
    {
        StringBuilder sb = new StringBuilder();
        DateTime startTime = DateTime.Now;

        while (true)
        {
            if (stream.DataAvailable)
            {
                int data = stream.ReadByte();
                if (data == -1) // End of stream
                    break;
                if (data == '\n')
                    break;
                sb.Append((char)data);
            }
            else
            {
                // Check if we've exceeded the timeout
                if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMilliseconds)
                    throw new TimeoutException("Timeout while reading from the network stream.");
                
                // Short sleep to prevent CPU spinning
                System.Threading.Thread.Sleep(10);
            }
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
