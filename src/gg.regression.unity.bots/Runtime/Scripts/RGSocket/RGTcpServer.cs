using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using JetBrains.Annotations;
using RegressionGames.RemoteOrchestration.Models;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.BotSegments.Models;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RegressionGames
{
    public class RGTcpServer : MonoBehaviour
    {
        private String _assetsDirectory;

        private TcpListener _server;
        private Thread _listenerThread;
        private bool _isRunning;
        
        // client + any queued message to send to that client
        private ConcurrentDictionary<TcpClient, ConcurrentQueue<String>> _connectedClients = new ConcurrentDictionary<TcpClient, ConcurrentQueue<string>>();

        // the last active sequence that was sent to the client
        private BotSegmentsPlaybackController _botSegmentsPlaybackController;
        private ActiveSequence _activeSequence;
        
        
        [Serializable]
        private class IncomingMessage
        {
            // public SocketRequestType type;
            [CanBeNull] public String payload; // JSON string
        }
        
        /// <summary>
        /// Starts a new TCP server on a background thread
        /// </summary>
        private void Start()
        {
            _assetsDirectory = Path.GetFullPath("Assets/RegressionGames");
            _botSegmentsPlaybackController = FindObjectOfType<BotSegmentsPlaybackController>();
            
            _server = new TcpListener(IPAddress.Parse("127.0.0.1"), 8085);
            _server.Start();
            _isRunning = true;
            
            _listenerThread = new Thread(ListenForClients);
            _listenerThread.IsBackground = true;
            _listenerThread.Start();
        }

        private void Update()
        {
            var activeSequence = GetActiveBotSequence();
            if (activeSequence?.resourcePath != _activeSequence?.resourcePath)
            {
                _activeSequence = activeSequence;
                var message = "{\"type\":\"ACTIVE_SEQUENCE\",\"body\":{\"sequence\":" + JsonUtility.ToJson(_activeSequence) + "}}";
                foreach (var messageQueue in _connectedClients.Values)
                {
                    messageQueue.Enqueue(message);
                }
            }
        }
        
        [MenuItem("Regression Games/Open Dashboard")]
        public static void OpenRGDashboard()
        {
            // Configure the process
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Path.GetFullPath("Packages/gg.regression.unity.bots/Runtime/Resources/RegressionGames.exe"),
                Arguments = "Extra args to pass to the program",
                UseShellExecute = false, 
                CreateNoWindow = false
            };
            Process.Start(startInfo);
        }
        
        /// <summary>
        /// Listens for client connections and handles them on separate threads
        /// </summary>
        private void ListenForClients()
        {
            while (_isRunning)
            {
                if (_server.Pending())
                {
                    // assume only one client connection at a time
                    // but need to listen for new connections in case the client disconnects
                    TcpClient client = _server.AcceptTcpClient();
                    _connectedClients.TryAdd(client, new ConcurrentQueue<string>());
                    Debug.Log("Client connected");
                    
                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }

                Thread.Sleep(10); // Prevents tight loop
            }
        }
        
        private int GetAvailablePort()
        {
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error finding an available port: {ex.Message}");
                return 0;
            }
            finally
            {
                listener?.Stop();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void HandleClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                bool handshakeReceived = false;
                
                // enter to an infinite cycle to be able to handle every change in stream
                while (_isRunning)
                {
                    if (handshakeReceived)
                    {
                        // send any pending messages to the client
                        if (_connectedClients.TryGetValue(client, out var messages))
                        {
                            while (messages.TryDequeue(out var message))
                            {
                                SendPayload(stream, message);
                            }
                        }
                    }

                    if (!stream.DataAvailable && client.Available < 3)
                    {
                        continue;
                    }
                    
                    byte[] bytes = new byte[client.Available];
                    stream.Read(bytes, 0, bytes.Length);
                    string s = Encoding.UTF8.GetString(bytes);

                    if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase))
                    {
                        Debug.Log($"=====Handshaking from client=====\n{s}");

                        // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                        // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                        // 3. Compute SHA-1 and Base64 hash of the new value
                        // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                        string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                        string swkAndSalt = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                        byte[] swkAndSaltSha1 = System.Security.Cryptography.SHA1.Create()
                            .ComputeHash(Encoding.UTF8.GetBytes(swkAndSalt));
                        string swkAndSaltSha1Base64 = Convert.ToBase64String(swkAndSaltSha1);

                        // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                        byte[] response = Encoding.UTF8.GetBytes(
                            "HTTP/1.1 101 Switching Protocols\r\n" +
                            "Connection: Upgrade\r\n" +
                            "Upgrade: websocket\r\n" +
                            "Sec-WebSocket-Accept: " + swkAndSaltSha1Base64 + "\r\n\r\n");

                        stream.Write(response, 0, response.Length);
                        handshakeReceived = true;
                    }
                    else
                    {
                        bool fin = (bytes[0] & 0b10000000) !=
                                   0; // whether the full message has been sent from the client
                        bool mask = (bytes[1] & 0b10000000) !=
                                    0; // must be true, "All messages from the client to the server have this bit set"
                        int opcode = bytes[0] & 0b00001111; // expecting 1 - text message
                        ulong offset = 2;
                        ulong msglen = bytes[1] & (ulong)0b01111111;

                        if (msglen == 126)
                        {
                            // bytes are reversed because websocket will print them in Big-Endian, whereas
                            // BitConverter will want them arranged in little-endian on windows
                            msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                            offset = 4;
                        }
                        else if (msglen == 127)
                        {
                            // To test the below code, we need to manually buffer larger messages — since the NIC's autobuffering
                            // may be too latency-friendly for this code to run (that is, we may have only some of the bytes in this
                            // websocket frame available through client.Available).
                            msglen = BitConverter.ToUInt64(
                                new byte[]
                                {
                                    bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2]
                                }, 0);
                            offset = 10;
                        }

                        if (msglen == 0)
                        {
                            Debug.Log("msglen == 0");
                        }
                        else if (mask)
                        {
                            byte[] decoded = new byte[msglen];
                            byte[] masks = new byte[4]
                                { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                            offset += 4;

                            for (ulong i = 0; i < msglen; ++i)
                            {
                                decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);
                            }

                            string message = Encoding.UTF8.GetString(decoded);
                            Debug.Log($"Received: {message}");

                            // Process the payload
                            var jsonObject = JsonUtility.FromJson<IncomingMessage>(message);
                            // switch (jsonObject.type)
                            // {
                            //     case SocketRequestType.PING: SendPayload(stream, "{\"type\":\"PONG\"}"); break;
                            //     case SocketRequestType.PING.ECHO: SendPayload(stream, "{\"type\":\"ECHO\",\"body\":\"" + jsonObject.body + "\"}"); break;
                            // }
                        }
                        else
                        {
                            Debug.Log("mask bit not set");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling client: {ex.Message}");
            }
            
            client.Close();
            _connectedClients.TryRemove(client, out _);
            Debug.Log("Client disconnected");
        }

        private void SendPayload(NetworkStream stream, string payload)
        {
            try
            {
                byte[] responseBytes = EncodeMessageToSend(payload);
                stream.Write(responseBytes, 0, responseBytes.Length);
                stream.Flush();
                Debug.Log($"Sent: {payload}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error sending payload: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Encode message from server -> client
        /// </summary>
        private byte[] EncodeMessageToSend(String message)
        {
            byte[] response;
            byte[] bytesRaw = Encoding.UTF8.GetBytes(message);
            byte[] frame = new byte[10];

            Int32 indexStartRawData = -1;
            Int32 length = bytesRaw.Length;

            frame[0] = 129;
            if (length <= 125)
            {
                frame[1] = (Byte)length;
                indexStartRawData = 2;
            }
            else if (length >= 126 && length <= 65535)
            {
                frame[1] = (Byte)126;
                frame[2] = (Byte)((length >> 8) & 255);
                frame[3] = (Byte)(length & 255);
                indexStartRawData = 4;
            }
            else
            {
                frame[1] = (byte)127;
                frame[2] = (byte)((length >> 56) & 255);
                frame[3] = (byte)((length >> 48) & 255);
                frame[4] = (byte)((length >> 40) & 255);
                frame[5] = (byte)((length >> 32) & 255);
                frame[6] = (byte)((length >> 24) & 255);
                frame[7] = (byte)((length >> 16) & 255);
                frame[8] = (byte)((length >> 8) & 255);
                frame[9] = (byte)(length & 255);

                indexStartRawData = 10;
            }

            response = new byte[indexStartRawData + length];

            Int32 i, reponseIdx = 0;

            //Add the frame bytes to the response
            for (i = 0; i < indexStartRawData; i++)
            {
                response[reponseIdx] = frame[i];
                reponseIdx++;
            }

            //Add the data bytes to the response
            for (i = 0; i < length; i++)
            {
                response[reponseIdx] = bytesRaw[i];
                reponseIdx++;
            }

            return response;
        }
        
        public ActiveSequence GetActiveBotSequence()
        {
            if (_botSegmentsPlaybackController != null && _botSegmentsPlaybackController.GetState() != PlayState.NotLoaded)
            {
                if (_botSegmentsPlaybackController.GetState() == PlayState.Playing || _botSegmentsPlaybackController.GetState() == PlayState.Starting || (_botSegmentsPlaybackController.GetState() == PlayState.Stopped && _botSegmentsPlaybackController.ReplayCompletedSuccessfully() == null && _botSegmentsPlaybackController.GetLastSegmentPlaybackWarning() == null))
                {
                    // a group of segments is playing.. let's see if we can figure out more details or not
                    if (BotSequence.ActiveBotSequence != null)
                    {
                        // this is a bot sequence, give them the name and path
                        return new ActiveSequence()
                        {
                            name = BotSequence.ActiveBotSequence.name,
                            description = BotSequence.ActiveBotSequence.description,
                            resourcePath = BotSequence.ActiveBotSequence.resourcePath,
                        };
                    }

                    // else - a zip file or other bot segments are running outside of a sequence
                    return new ActiveSequence()
                    {
                        name = "BotSegments are active outside of a BotSequence",
                        description = "BotSegment(s) are active outside of a BotSequence.  This happens when a user is testing individual BotSegments or BotSegmentLists from the overlay, or when a replay is running from a .zip file.",
                        resourcePath = "",
                    };
                }
            }

            return null;
        }
        
        private void OnApplicationQuit()
        {
            _isRunning = false;
            _server?.Stop();
            _listenerThread?.Join();
            Debug.Log("Server stopped");
        }
    }
}

