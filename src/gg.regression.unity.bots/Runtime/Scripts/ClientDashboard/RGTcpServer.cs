using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using JetBrains.Annotations;
using Newtonsoft.Json;
using RegressionGames.StateRecorder;
using UnityEngine;

namespace RegressionGames.ClientDashboard
{
    // ReSharper disable InconsistentNaming
    public class RGTcpServer 
    {
        public static bool IsRunning => m_isRunning;

        private static TcpListener m_server;
        private static Thread m_listenerThread;
        private static CancellationTokenSource m_cancellationTokenSource;
        private static bool m_isRunning;
        private static readonly ConcurrentDictionary<TcpClient, ClientActions> m_connectedClients = new ();
        
        /// <summary>
        /// Contains queued messages for a connected client,
        /// and whether this server needs to close the client connection
        /// </summary>
        private class ClientActions
        {
            public readonly ConcurrentQueue<TcpMessage> QueuedMessages = new ();
            public bool ShouldClose = false;
        }
        
        #region Public Methods
        
        /// <summary>
        /// Create a TCPListener and begin listening for client connections on a separate thread
        /// </summary>
        public static void Start()
        {
            if (m_isRunning)
            {
                return;
            }
        
            m_server = new TcpListener(IPAddress.Parse("127.0.0.1"), 8085);
            m_server.Start();
            m_isRunning = true;
        
            m_cancellationTokenSource = new CancellationTokenSource();
            m_listenerThread = new Thread(() =>
            {
                ListenForClientConnections(m_cancellationTokenSource.Token);
            });
            m_listenerThread.IsBackground = true;
            m_listenerThread.Start();
        }
        
        /// <summary>
        /// Queue a message to send to a specific client, or all connected clients if no client is defined.
        /// </summary>
        public static void QueueMessage(TcpMessage message, [CanBeNull] TcpClient client = null)
        {
            if (client == null)
            {
                foreach (var clientActions in m_connectedClients.Values)
                {
                    clientActions.QueuedMessages.Enqueue(message);
                }
            }
            else
            {
                if (m_connectedClients.TryGetValue(client, out var clientActions))
                {
                    clientActions.QueuedMessages.Enqueue(message);
                }
            }
        }
        
        /// <summary>
        /// Stop the TCP server and close all client connections
        /// </summary>
        public static void Stop()
        {
            if (!m_isRunning)
            {
                return;
            }
            
            // send application quit messages to all connected clients then close them
            foreach (var client in m_connectedClients.Keys)
            {
                if (!client.Connected) continue;
                var message = new TcpMessage { type = TcpMessageType.CloseConnection };
                SendMessage(client, message);
                client.Close();
            }
            m_connectedClients.Clear();
            
            // then stop server
            m_isRunning = false;
            m_server?.Stop();
            m_cancellationTokenSource?.Cancel();
            m_listenerThread?.Join();
        }
        
        #endregion
        
        #region Overridable Callbacks
        
        /// <summary>
        /// Called when the client handshake is completed successfully
        /// </summary>
        public static event OnClientHandshakeHandler OnClientHandshake;
        public delegate void OnClientHandshakeHandler(TcpClient client);
        
        /// <summary>
        /// Called when a message is received from a client
        /// </summary>
        public static event ProcessClientMessageHandler ProcessClientMessage;
        public delegate void ProcessClientMessageHandler(TcpClient client, TcpMessage message);
        
        #endregion
        
        #region Connection Management
        
        /// <summary>
        /// Listens for client connections and handles them on separate threads
        /// </summary>
        private static void ListenForClientConnections(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    TcpClient client = m_server.AcceptTcpClient();
                    
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    m_connectedClients.TryAdd(client, new ClientActions());

                    Thread clientThread = new Thread(() => HandleClientCommunication(client, token));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
            }
            catch (SocketException e)
            {
                if (!m_isRunning || token.IsCancellationRequested)
                {
                    // we just closed the socket, so eat this exception
                }
                else
                {
                    RGDebug.LogError($"Error communicating with dashboard: {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// Handles all communication between the client and server including handshake
        /// </summary>
        private static void HandleClientCommunication(TcpClient client, CancellationToken token)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                bool handshakeReceived = false;
                
                // enter to an infinite cycle to be able to handle every change in stream
                while (!token.IsCancellationRequested)
                {
                    // check if we need to close the client connection
                    if (m_connectedClients.TryGetValue(client, out var clientActions) && clientActions.ShouldClose)
                    {
                        break;
                    }
                    
                    if (!handshakeReceived)
                    {
                        handshakeReceived = TryPerformHandshake(client);
                        if (handshakeReceived)
                        {
                            OnClientHandshake?.Invoke(client);
                        }
                        continue;
                    }
                    
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    // send any queued messages before handling new messages
                    SendQueuedMessages(client);

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }
                    
                    if (!stream.DataAvailable)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    var received = DecodeReceivedMessage(client);
                    if (received != null)
                    {
                        var deserializedMessage = JsonConvert.DeserializeObject<TcpMessage>(received, JsonUtils.JsonSerializerSettings);
                        ProcessClientMessage?.Invoke(client, deserializedMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is not ThreadAbortException)
                {
                    RGDebug.LogError($"Error handling message from dashboard: {ex.Message}");
                }
            }
            
            client.Close();
            m_connectedClients.TryRemove(client, out _);
        }
        
        #endregion

        #region Processing and Sending Messages
        
        /// <summary>
        /// Waits for http upgrade request from client to establish websocket connection
        /// https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server#handshaking
        /// </summary>
        private static bool TryPerformHandshake(TcpClient client)
        {
            var stream = client.GetStream();
            if (!stream.DataAvailable || client.Available < 3)
            {
                return false;
            }
                    
            byte[] bytes = new byte[client.Available];
            stream.Read(bytes, 0, bytes.Length);
            string message = Encoding.UTF8.GetString(bytes);

            if (!Regex.IsMatch(message, "^GET", RegexOptions.IgnoreCase))
            {
                return false;
            }

            // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
            // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
            // 3. Compute SHA-1 and Base64 hash of the new value
            // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
            string swk = Regex.Match(message, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
            string swkAndSalt = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            byte[] swkAndSaltSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swkAndSalt));
            string swkAndSaltSha1Base64 = Convert.ToBase64String(swkAndSaltSha1);

            // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
            byte[] response = Encoding.UTF8.GetBytes(
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Connection: Upgrade\r\n" +
                "Upgrade: websocket\r\n" +
                "Sec-WebSocket-Accept: " + swkAndSaltSha1Base64 + "\r\n\r\n");

            stream.Write(response, 0, response.Length);
            return true;
        }
        
        /// <summary>
        /// Send any queued messages to connected clients
        /// </summary>
        private static void SendQueuedMessages(TcpClient client)
        {
            // send any pending messages to the client
            if (m_connectedClients.TryGetValue(client, out var clientActions))
            {
                while (clientActions.QueuedMessages.TryDequeue(out var message))
                {
                    SendMessage(client, message);
                }
            }
        }
        
        /// <summary>
        /// Send a single message to a client
        /// </summary>
        private static void SendMessage(TcpClient client, TcpMessage message)
        {
            try
            {
                var stream = client.GetStream();
                var jsonMessage = message.ToString();
                var responseBytes = EncodeMessageToSend(jsonMessage);
                stream.Write(responseBytes, 0, responseBytes.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                RGDebug.LogError($"Error sending message to dashboard: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Decode message from client so we can process it
        /// https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_servers#exchanging_data_frames
        /// https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server#decoding_messages
        /// </summary>
        private static string DecodeReceivedMessage(TcpClient client)
        {
            var clientStream = client.GetStream();
            var decodedBytes = new List<byte>();

            while (client.Connected)
            {
                if (client.Available == 0)
                {
                    // we check this condition before calling this method,
                    // but we may be waiting on a continuation frame here
                    continue;
                }
                
                // read the first byte of the message
                // bit 0 -> fin, whether the full message has been received
                // bit 1-3 -> reserved, we don't care about this right now
                // bit 4-7 -> opcode, the type of message
                var firstByte = (byte)clientStream.ReadByte();
                bool fin = (firstByte & 0b10000000) != 0;
                int frameOpcode = firstByte & 0b00001111;
                
                switch (frameOpcode)
                {
                    case 0x0: // Continuation Frame
                    case 0x1: // Text
                    case 0x2: // Binary  
                        break;
                    case 0x8: // Connection close
                    {
                        if (m_connectedClients.TryGetValue(client, out var clientActions))
                        {
                            clientActions.ShouldClose = true;
                        }
                        return null;
                    }
                    default:  // Ping/Pong/Reserved
                        continue;
                }
                
                // read the second byte of the message
                // bit 0 -> mask, should be true for client > server messages
                // bit 1-7 -> payload length
                var secondByte = (byte)clientStream.ReadByte();
                bool mask = (secondByte & 0b10000000) != 0;
                var psuedoLength = secondByte - 128;

                // to get the actual length of the payload, we need to interpret the psuedo length from the header
                int payloadLength = 0;

                if (psuedoLength > 0 && psuedoLength <= 125)
                {
                    // if length doesn't exceed 125,
                    // then this is the actual length of the payload
                    payloadLength = psuedoLength;
                }
                else if (psuedoLength == 126)
                {
                    // Actual length will be the next 2 bytes
                    var lengthBytes = new byte[2];
                    clientStream.Read(lengthBytes, 0, lengthBytes.Length);
                    
                    // bytes are reversed because websocket will print them in Big-Endian, whereas
                    // BitConverter will want them arranged in little-endian on windows
                    payloadLength = BitConverter.ToUInt16(new byte[] { lengthBytes[1], lengthBytes[0] }, 0);
                } 
                else if (psuedoLength == 127)
                {
                    // Actual length will be the next 8 bytes
                    var lengthBytes = new byte[8];
                    clientStream.Read(lengthBytes, 0, lengthBytes.Length);
                    
                    // bytes are reversed because websocket will print them in Big-Endian, whereas
                    // BitConverter will want them arranged in little-endian on windows
                    payloadLength = (int)BitConverter.ToUInt64(new byte[] {
                        lengthBytes[7], lengthBytes[6], lengthBytes[5], lengthBytes[4],
                        lengthBytes[3], lengthBytes[2], lengthBytes[1], lengthBytes[0]
                    }, 0);
                }

                // for now we're assuming client-sent messages are masked
                // determine the masking key from the next 4 bytes
                // this will be used to decode the message
                var maskingKey = new byte[4];
                clientStream.Read(maskingKey, 0, maskingKey.Length);
                
                if (payloadLength == 0)
                {
                    // no payload, so we're done
                    return null;
                }
                
                // read the payload
                // and decode it using the masking key
                var payload = new byte[payloadLength];
                clientStream.Read(payload, 0, payloadLength);
                
                for (int i = 0; i < payload.Length; ++i)
                {
                    decodedBytes.Add((byte)(payload[i] ^ maskingKey[i % 4]));
                }

                if (!fin)
                {
                    // if this isn't the complete message,
                    // then we need to wait for the continuation frame
                    continue;
                }
                
                // we have the full message, so return it as a string
                return Encoding.UTF8.GetString(decodedBytes.ToArray());
            }

            return null;
        }
        
        /// <summary>
        /// Encode message from server -> client
        /// https://stackoverflow.com/questions/8125507/how-can-i-send-and-receive-websocket-messages-on-the-server-side/27442080#27442080
        /// </summary>
        private static byte[] EncodeMessageToSend(string message)
        {
            byte[] bytesRaw = Encoding.UTF8.GetBytes(message);
            byte[] frame = new byte[10];

            int indexStartRawData = -1;
            int length = bytesRaw.Length;

            frame[0] = 129;
            if (length <= 125)
            {
                frame[1] = (byte)length;
                indexStartRawData = 2;
            }
            else if (length >= 126 && length <= 65535)
            {
                frame[1] = (byte)126;
                frame[2] = (byte)((length >> 8) & 255);
                frame[3] = (byte)(length & 255);
                indexStartRawData = 4;
            }
            else
            {
                var lengthAsULong = Convert.ToUInt64(length);
                frame[1] = 127;
                frame[2] = (byte)((lengthAsULong >> 56) & 255);
                frame[3] = (byte)((lengthAsULong >> 48) & 255);
                frame[4] = (byte)((lengthAsULong >> 40) & 255);
                frame[5] = (byte)((lengthAsULong >> 32) & 255);
                frame[6] = (byte)((lengthAsULong >> 24) & 255);
                frame[7] = (byte)((lengthAsULong >> 16) & 255);
                frame[8] = (byte)((lengthAsULong >> 8) & 255);
                frame[9] = (byte)(lengthAsULong & 255);
                indexStartRawData = 10;
            }

            byte[] response = new byte[indexStartRawData + length];
            int i, responseIdx = 0;

            // Add the frame bytes to the response
            for (i = 0; i < indexStartRawData; i++)
            {
                response[responseIdx] = frame[i];
                responseIdx++;
            }

            // Add the data bytes to the response
            for (i = 0; i < length; i++)
            {
                response[responseIdx] = bytesRaw[i];
                responseIdx++;
            }

            return response;
        }

        #endregion
    }
}