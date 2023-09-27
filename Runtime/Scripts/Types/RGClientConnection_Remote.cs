using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RegressionGames.Types
{
    public class RGClientConnection_Remote : RGClientConnection
    {
        [CanBeNull] private TcpClient _client;
        [CanBeNull] private RGBotInstanceExternalConnectionInfo _connectionInfo;

        private bool _connecting = false;

        public RGClientConnection_Remote(uint clientId, string lifecycle = "MANAGED",
            [CanBeNull] RGBotInstanceExternalConnectionInfo connectionInfo = null, [CanBeNull] TcpClient client = null) : base(clientId, RGClientConnectionType.REMOTE, lifecycle)
        {
            this._client = client;
            this._connectionInfo = connectionInfo;
        }

        public override bool SendTickInfo(RGTickInfoData tickInfo)
        {
            if (SendSocketMessage("tickInfo", tickInfo.ToString()))
            {
                RGBotServerListener.GetInstance()?.SetUnityBotState(ClientId, RGUnityBotState.RUNNING);
                return true;
            }

            return false;
        }

        public override bool SendTeardown()
        {
            return SendSocketMessage( "teardown", JsonUtility.ToJson("{}"));
        }

        public override bool SendHandshakeResponse(RGServerHandshake handshake)
        {
            return SendSocketMessage( "handshake", JsonUtility.ToJson(handshake));
        }

        private bool SendSocketMessage(string type, string data)
        {
            if (Connected() && _client != null)
            {
                RGServerSocketMessage serverSocketMessage =
                    new RGServerSocketMessage(Token, type, data);
                byte[] dataBuffer = Encoding.UTF8.GetBytes(JsonUtility.ToJson(serverSocketMessage));
                byte[] finalBuffer = new byte[4 + dataBuffer.Length];
                // put the length header into the buffer first
                BinaryPrimitives.WriteInt32BigEndian(finalBuffer, dataBuffer.Length);
                Array.Copy(dataBuffer, 0, finalBuffer, 4, dataBuffer.Length);
                ValueTask vt = _client.GetStream().WriteAsync(finalBuffer);
                vt.ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
                {
                    if (!vt.IsCompletedSuccessfully)
                    {
                        RGDebug.LogDebug($"Client Id: {ClientId} socket error or closed, need to re-establish connection for bot");
                        // client got pulled out from under us or restarted/reloaded.. handle it on the next Update
                        try
                        {
                            Close();
                        }
                        catch (Exception ex)
                        {
                        }

                        // we could despawn their avatar, but not remove them from the lobby
                        // however we don't do this as we lose the avatar position/playerclass to
                        // restore on re-connect
                        //RGBotSpawnManager.GetInstance()?.DeSpawnBot(clientId);
                    }
                });
                return true;
            }

            return false;
        }

        public override bool Connected()
        {
            if (_client != null && _connectionInfo != null) {
                if (_client?.Client?.RemoteEndPoint is IPEndPoint ep && ep.Port == _connectionInfo.port &&
                    AddressesEqual(ep.Address.ToString(), _connectionInfo.address))
                {
                    return _client.Connected;
                }
            }

            // not connected or port/address mis-match.. need to re-connect
            return false;
        }

        public override async void Connect()
        {
            if (!_connecting && !Connected())
            {
                RGBotServerListener.GetInstance()?.SetUnityBotState(ClientId, RGUnityBotState.CONNECTING);
                _connecting = true;
                RGDebug.LogDebug(
                    $"Getting external connection information for botInstanceId: {ClientId}");
                await RGServiceManager.GetInstance()?.GetExternalConnectionInformationForBotInstance(
                    (long)ClientId,
                    (connInfo) => { _connectionInfo = connInfo; },
                    () => { _connecting = false; }
                )!;
                var _this = this;
                await Task.Run(() =>
                {
                    // make sure we only setup 1 connection at a time on this connection object
                    lock (_this)
                    {
                        if (_client == null && _connectionInfo != null)
                        {
                            // make sure we were able to get the current connection info
                            TcpClient client = new TcpClient();
                            // create a new TcpClient, then start a connect attempt asynchronously
                            string address = _connectionInfo.address;
                            int port = _connectionInfo.port;

                            _client = client;
                            RGDebug.LogInfo(
                                $"Connecting to bot at {address}:{port} for ClientId: {ClientId}");
                            IAsyncResult beginConnect = client.BeginConnect(address, port, ar =>
                            {
                                if (_connecting)
                                {
                                    lock (_this)
                                    {
                                        if (_connecting)
                                        {
                                            // nodejs side should start handshakes/etc
                                            // we just need to save our connection reference
                                            try
                                            {
                                                client.EndConnect(ar);
                                                _connecting = false;
                                                HandleClientConnection(client);
                                            }
                                            catch (Exception ex)
                                            {
                                                // this is debug because of how we have to retry frequently when connecting bots
                                                RGDebug.LogDebug(
                                                    $"WARNING: Failed to connect bot TCP socket to {address}:{port} - {ex.Message}");
                                                // mark this connection as needing to try again on a future update
                                                try
                                                {
                                                    client.EndConnect(ar);
                                                }
                                                catch (Exception e1)
                                                {
                                                    // may not have gotten far enough to do this
                                                }

                                                try
                                                {
                                                    client.Close();
                                                }
                                                catch (Exception e1)
                                                {
                                                    // may not have gotten far enough to do this
                                                }
                                            }
                                            finally
                                            {
                                                _connecting = false;
                                            }
                                        }
                                    }
                                }

                            }, null);

                            // start a timer for 5 seconds from now that will cancel the connection attempt if it didn't connect yet
                            System.Timers.Timer t = new System.Timers.Timer(5000);
                            t.Elapsed += (s, e) =>
                            {
                                // see if we need to cancel the connect
                                if (_connecting)
                                {
                                    lock (_this)
                                    {
                                        if (_connecting)
                                        {
                                            RGDebug.LogInfo(
                                                $"Connection TIMED OUT to bot at {address}:{port} for ClientId: {ClientId}");
                                            try
                                            {
                                                client.EndConnect(beginConnect);
                                            }
                                            catch (Exception e1)
                                            {
                                                // may not have gotten far enough to do this
                                            }
                                            

                                            try
                                            {
                                                client.Close();
                                            }
                                            catch (Exception e1)
                                            {
                                                // may not have gotten far enough to do this
                                            }
                                            _connecting = false;
                                        }
                                    }
                                }
                            };
                            t.AutoReset = false;
                            t.Start();
                        }
                        else
                        {
                            _connecting = false;
                        }
                    }
                });
            }
        }

        public override void Close()
        {
            this._client?.Close();
            this._client = null;
        }
        
        private Task HandleClientConnection(TcpClient client)
        {
            return Task.Run(() =>
            {
                RGDebug.LogDebug($"TcpClient socket connected - {client.Connected}");
                int socketMessageLength = 0;
                int socketHeaderBytesReceived = 0;
                byte[] socketHeaderBytes = new byte[4];
                string socketState = "header";
                byte[] socketMessageBytes = new byte[0];
                int socketMessageBytesReceived = 0;

                // loop reading data
                while (client.Connected)
                {
                    byte[] byteBuffer = new byte[1024];
                    NetworkStream socketStream = client.GetStream();
                    int i = socketStream.Read(byteBuffer, 0, byteBuffer.Length);
                    //RGDebug.LogVerbose($"Read {i} bytes from client socket");
                    if (i > 0)
                    {
                        int bufferIndex = 0;
                        while (bufferIndex < i)
                        {
                            switch (socketState)
                            {
                                case "header":
                                    if (socketHeaderBytesReceived < 4)
                                    {
                                        // copy the data into the header bytes
                                        int headerBytesToCopy = Math.Min(4 - socketHeaderBytesReceived,
                                            i - bufferIndex);
                                        Array.Copy(byteBuffer, bufferIndex, socketHeaderBytes,
                                            socketHeaderBytesReceived, headerBytesToCopy);
                                        bufferIndex += headerBytesToCopy;
                                        socketHeaderBytesReceived += headerBytesToCopy;
                                    }

                                    if (socketHeaderBytesReceived == 4)
                                    {
                                        socketState = "data";
                                        socketHeaderBytesReceived = 0;
                                        socketMessageLength =
                                            BinaryPrimitives.ReadInt32BigEndian(socketHeaderBytes);
                                        socketMessageBytesReceived = 0;
                                        socketMessageBytes = new byte[socketMessageLength];
                                    }

                                    break;
                                case "data":
                                    // copy the data into the message array
                                    int dataBytesToCopy = Math.Min(socketMessageLength - socketMessageBytesReceived,
                                        i - bufferIndex);
                                    Array.Copy(byteBuffer, bufferIndex, socketMessageBytes,
                                        socketMessageBytesReceived, dataBytesToCopy);

                                    bufferIndex += dataBytesToCopy;
                                    socketMessageBytesReceived += dataBytesToCopy;

                                    if (socketMessageBytesReceived == socketMessageLength)
                                    {
                                        socketState = "header";

                                        string sockMessage = Encoding.UTF8.GetString(socketMessageBytes);
                                        // handle the message
                                        HandleSocketMessage(sockMessage);
                                        socketHeaderBytesReceived = 0;
                                        socketMessageLength = 0;
                                        socketMessageBytesReceived = 0;
                                    }

                                    break;
                            }
                        }
                    }
                }

                if (!client.Connected)
                {
                    // TODO: Handle de-spawning their avatar and/or removing them from the lobby?
                }
            });
        }
        
        private void HandleSocketMessage(string message)
        {
            RGDebug.LogDebug($"Processing socket message from client, message: {message}");
            RGClientSocketMessage clientSocketMessage = JsonUtility.FromJson <RGClientSocketMessage> (message);

            string type = clientSocketMessage.type;
            string token = clientSocketMessage.token;
            uint clientId = clientSocketMessage.clientId;
            string data = clientSocketMessage.data;

            switch (type)
            {
                case "handshake":
                    HandleClientHandshakeMessage(clientId, data);
                    break;
                case "validationResult":
                    HandleClientValidationResultMessage(clientId, token, data);
                    break;
                case "request":
                    HandleClientRequestMessage(clientId, token, data);
                    break;
                case "teardown":
                    RGBotServerListener.GetInstance()?.HandleClientTeardown(clientId);
                    break;
            }

        }
        
        private void HandleClientHandshakeMessage(uint clientId, string data)
        {
            RGClientHandshake handshakeMessage = JsonUtility.FromJson<RGClientHandshake>(data);
            //token check is handled in this method call
            RGBotServerListener.GetInstance()?.HandleClientHandshakeMessage(clientId, handshakeMessage);
        }
        
        private void HandleClientValidationResultMessage(uint clientId, string token, string data)
        {
            if (CheckAccessToken(clientId, token) == true)
            {
                var validationResult = JsonConvert.DeserializeObject<RGValidationResult>(data);
                RGBotServerListener.GetInstance()?.HandleClientValidationResult(clientId, validationResult);
            }
        }
        
        private void HandleClientRequestMessage(uint clientId, string token, string data)
        {
            if (CheckAccessToken(clientId, token) == true)
            {
                var actionRequest = JsonConvert.DeserializeObject<RGActionRequest>(data);
                RGBotServerListener.GetInstance()?.HandleClientActionRequest(clientId, actionRequest);
            }
        }
        
        private static bool CheckAccessToken(uint clientId, string clientToken)
        {
            if (clientToken.Equals(RGBotServerListener.GetInstance()?.UnitySideToken))
            {
                return true;
            }
            RGDebug.LogWarning($"WARNING: Client id {clientId} made call with invalid token");
            return false;

        }

        private static bool AddressesEqual(string address1, string address2)
        {
            // normalize localhost
            if (address1 == "127.0.0.1")
            {
                address1 = "localhost";
            }
            
            if (address2 == "127.0.0.1")
            {
                address2 = "localhost";
            }

            return address1.Equals(address2);
        }
    }
    
}
