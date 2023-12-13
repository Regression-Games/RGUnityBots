using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateActionTypes;
using UnityEngine;
using Timer = System.Timers.Timer;

namespace RegressionGames.Types
{
    public class RGClientConnection_Remote : RGClientConnection
    {
        [CanBeNull] private TcpClient _client;

        private const int SOCKET_READWRITE_TIMEOUT = 5_000; // 5 seconds

        private SemaphoreSlim _connecting = new (1,1);
        [CanBeNull] private RGBotInstanceExternalConnectionInfo _connectionInfo;

        public RGClientConnection_Remote(long clientId, string lifecycle = "MANAGED",
            [CanBeNull] RGBotInstanceExternalConnectionInfo connectionInfo = null, [CanBeNull] TcpClient client = null)
            : base(clientId, RGClientConnectionType.REMOTE, lifecycle)
        {
            _client = client;
            _connectionInfo = connectionInfo;
        }

        public override bool SendTickInfo(RGTickInfoData tickInfo)
        {
            if (SendSocketMessage("tickInfo", tickInfo.ToString()))
            {
                return true;
            }

            return false;
        }

        public override bool SendTeardown()
        {
            return SendSocketMessage("teardown", JsonUtility.ToJson("{}"));
        }

        public override bool SendHandshakeResponse(RGServerHandshake handshake)
        {
            return SendSocketMessage("handshake", JsonUtility.ToJson(handshake));
        }

        private bool SendSocketMessage(string type, string data)
        {
            if (Connected() && _client != null)
            {
                var dataBuffer = Encoding.UTF8.GetBytes(
                    JsonUtility.ToJson(
                        new RGServerSocketMessage(Token, type, data)
                    )
                );
                var finalBuffer = new byte[4 + dataBuffer.Length];
                // put the length header into the buffer first
                BinaryPrimitives.WriteInt32BigEndian(finalBuffer, dataBuffer.Length);
                Array.Copy(dataBuffer, 0, finalBuffer, 4, dataBuffer.Length);
                var canWrite = _client?.Connected == true && _client?.Client.Poll(1, SelectMode.SelectWrite) == true;
                if (canWrite)
                {
                    var vt = _client.GetStream().WriteAsync(finalBuffer);
                    vt.ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
                    {
                        // see if the socket is no longer writeable
                        if (!vt.IsCompletedSuccessfully)
                        {
                            HandleDeadWriteState();
                        }
                    });
                    return true;
                }
                else
                {
                    HandleDeadWriteState();
                }
            }

            return false;
        }

        private void HandleDeadWriteState()
        {
            // if client isn't already closing/closed
            if (_client != null)
            {
                lock (this)
                {
                    if (_client != null)
                    {
                        RGDebug.LogWarning(
                            $"Client Id: {ClientId} socket error or closed during write, need to re-establish connection for bot");
                        // client got pulled out from under us or restarted/reloaded.. handle it on the next Update
                        try
                        {
                            Close();
                        }
                        // ReSharper disable once EmptyGeneralCatchClause
                        catch (Exception)
                        {
                        }

                        // we could despawn their avatar, but not remove them from the lobby
                        // however we don't do this as we lose the avatar position/playerclass to
                        // restore on re-connect
                        //RGBotSpawnManager.GetInstance()?.DeSpawnBot(clientId);
                    }
                }
            }
        }

        public override bool Connected()
        {
            if (_client != null && _connectionInfo != null)
            {
                try
                {
                    if (_client?.Client?.RemoteEndPoint is IPEndPoint ep && ep.Port == _connectionInfo.port &&
                        AddressesEqual(ep.Address.ToString(), _connectionInfo.address))
                    {
                        return _client.Connected;
                    }
                }
                catch (Exception)
                {
                    // on teardown, the RemoteEndPoint can become invalid before the socket closes fully
                }
            }

            // not connected or port/address mis-match.. need to re-connect
            return false;
        }

        public override async void Connect()
        {
            var shouldIConnect = false;
            if (!Connected())
            {
                // see if the semaphore is currently available
                // we CANNOT do `.Wait()` here as we would block the main unity thread
                // calling us and the main thread must be available for Unity webrequests
                // that we do in this method to process
                // IOW.. it will deadlock the system
                var semaphoreAcquired = _connecting.Wait(1);
                if (semaphoreAcquired && !Connected())
                {
                    shouldIConnect = true;
                }

                if (shouldIConnect)
                {
                    RGBotServerListener.GetInstance()?.SetUnityBotState(ClientId, RGUnityBotState.CONNECTING);
                    RGDebug.LogDebug(
                        $"Getting external connection information for botInstanceId: {ClientId}");
                    await RGServiceManager.GetInstance()?.GetExternalConnectionInformationForBotInstance(
                        ClientId,
                        connInfo =>
                        {
                            _connectionInfo = connInfo;
                            // make sure we were able to get the current connection info
                            var client = new TcpClient();
                            client.ReceiveTimeout = SOCKET_READWRITE_TIMEOUT;
                            client.SendTimeout = SOCKET_READWRITE_TIMEOUT;
                            // create a new TcpClient, then start a connect attempt asynchronously
                            var address = _connectionInfo.address;
                            var port = _connectionInfo.port;

                            // client should be null, but close down existing just in case
                            if (_client != null)
                            {
                                Close();
                            }

                            _client = client;

                            var connectionComplete = 0; // 0 = false , 1+ = true

                            RGDebug.LogInfo(
                                $"Connecting to bot at {address}:{port} for ClientId: {ClientId}");
                            var beginConnect = client.BeginConnect(address, port, ar =>
                            {
                                // if == 1 , we got here before the timeout
                                if (Interlocked.Increment(ref connectionComplete) == 1)
                                {
                                    // nodejs side should start handshakes/etc
                                    // we just need to save our connection reference
                                    try
                                    {
                                        client.EndConnect(ar);
                                        RGDebug.LogDebug($"TcpClient socket connected - {client.Connected}");
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
                                        catch (Exception)
                                        {
                                            // may not have gotten far enough to do this
                                        }

                                        Close();
                                    }
                                    finally
                                    {
                                        _connecting.Release();
                                    }
                                }
                            }, null);

                            // start a timer for SOCKET_READWRITE_TIMEOUT * 2 ms from now that will cancel the connection attempt if it didn't connect yet
                            var t = new Timer(SOCKET_READWRITE_TIMEOUT * 2);
                            t.Elapsed += (s, e) =>
                            {
                                // if == 1 , we got here before the connection completed
                                if (Interlocked.Increment(ref connectionComplete) == 1)
                                {
                                    RGDebug.LogInfo(
                                        $"Connection TIMED OUT to bot at {address}:{port} for ClientId: {ClientId}");
                                    try
                                    {
                                        client.EndConnect(beginConnect);
                                    }
                                    catch (Exception)
                                    {
                                        // may not have gotten far enough to do this
                                        // RGDebug.LogDebug($"Failed to abort connection - {e1}");
                                    }

                                    Close();
                                    _connecting.Release();
                                }
                            };
                            t.AutoReset = false;
                            t.Start();
                        },
                    () =>
                        {
                            _connecting.Release();
                        }
                    );
                }
                else
                {
                    if (semaphoreAcquired)
                    {
                        _connecting.Release();
                    }
                }
            }
        }

        public override void Close()
        {
            try
            {
                _client?.Close();
            }
            catch (Exception)
            {
                // may not have gotten far enough to do this
            }

            _client = null;
        }

        private Dictionary<TcpClient, ClientConnectionState> connectionStates = new();

        private void HandleClientConnection(TcpClient client)
        {
            ClientConnectionState connectionState;
            if (!connectionStates.TryGetValue(client, out connectionState))
            {
                connectionState = new ClientConnectionState();
                connectionStates[client] = connectionState;
            }
            var byteBuffer = new byte[1024];
            var socketStream = client.GetStream();

            var readTask = socketStream.ReadAsync(byteBuffer, 0, byteBuffer.Length);
            readTask.ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
            {

                if (readTask.IsCompletedSuccessfully)
                {
                    var i = readTask.Result;
                    //RGDebug.LogVerbose($"Read {i} bytes from client socket");
                    if (i > 0)
                    {
                        var bufferIndex = 0;
                        while (bufferIndex < i)
                        {
                            switch (connectionState.socketState)
                            {
                                case "header":
                                    if (connectionState.socketHeaderBytesReceived < 4)
                                    {
                                        // copy the data into the header bytes
                                        var headerBytesToCopy = Math.Min(4 - connectionState.socketHeaderBytesReceived,
                                            i - bufferIndex);
                                        Array.Copy(byteBuffer, bufferIndex, connectionState.socketHeaderBytes,
                                            connectionState.socketHeaderBytesReceived, headerBytesToCopy);
                                        bufferIndex += headerBytesToCopy;
                                        connectionState.socketHeaderBytesReceived += headerBytesToCopy;
                                    }

                                    if (connectionState.socketHeaderBytesReceived == 4)
                                    {
                                        connectionState.socketState = "data";
                                        connectionState.socketHeaderBytesReceived = 0;
                                        connectionState.socketMessageLength =
                                            BinaryPrimitives.ReadInt32BigEndian(connectionState.socketHeaderBytes);
                                        connectionState.socketMessageBytesReceived = 0;
                                        connectionState.socketMessageBytes = new byte[connectionState.socketMessageLength];
                                    }

                                    break;
                                case "data":
                                    // copy the data into the message array
                                    var dataBytesToCopy = Math.Min(
                                        connectionState.socketMessageLength - connectionState.socketMessageBytesReceived,
                                        i - bufferIndex);
                                    Array.Copy(byteBuffer, bufferIndex, connectionState.socketMessageBytes,
                                        connectionState.socketMessageBytesReceived, dataBytesToCopy);

                                    bufferIndex += dataBytesToCopy;
                                    connectionState.socketMessageBytesReceived += dataBytesToCopy;

                                    if (connectionState.socketMessageBytesReceived == connectionState.socketMessageLength)
                                    {
                                        connectionState.socketState = "header";

                                        var sockMessage = Encoding.UTF8.GetString(connectionState.socketMessageBytes);
                                        // handle the message
                                        HandleSocketMessage(sockMessage);
                                        connectionState.socketHeaderBytesReceived = 0;
                                        connectionState.socketMessageLength = 0;
                                        connectionState.socketMessageBytesReceived = 0;
                                    }

                                    break;
                            }
                        }
                    }
                    // call the handler again
                    HandleClientConnection(client);
                }
                else
                {
                    // alert them in log if not tearing down connection on purpose
                    if (_client != null)
                    {
                        RGDebug.LogWarning(
                            $"Client Id: {ClientId} socket error or closed during read, need to re-establish connection for bot - {readTask.Exception}");
                    }
                    client.Close();
                        connectionStates.Remove(client);
                }
            });
        }

        private void HandleSocketMessage(string message)
        {
            RGDebug.LogDebug($"Processing socket message from client, message: {message}");
            var clientSocketMessage = JsonConvert.DeserializeObject<RGClientSocketMessage>(message);

            var type = clientSocketMessage.type;
            var token = clientSocketMessage.token;
            var clientId = clientSocketMessage.clientId;
            var data = clientSocketMessage.data;
            // helps JObject understand/parse the string better and fixes quotes on {}s for nested objects
            data = data?.Replace("\\\"", "\"").Replace("\"{", "{").Replace("}\"","}");

            var jObject = data == null ? null : JObject.Parse(data);

            switch (type)
            {
                case "handshake":
                    HandleClientHandshakeMessage(clientId, jObject);
                    break;
                case "validationResult":
                    HandleClientValidationResultMessage(clientId, token, jObject);
                    break;
                case "request":
                    HandleClientRequestMessage(clientId, token, jObject);
                    break;
                case "teardown":
                    RGBotServerListener.GetInstance()?.HandleClientTeardown(clientId);
                    break;
            }
        }

        private void HandleClientHandshakeMessage(long clientId, JObject data)
        {
            var handshakeMessage = data.ToObject<RGClientHandshake>();
            //token check is handled in this method call
            RGBotServerListener.GetInstance()?.HandleClientHandshakeMessage(clientId, handshakeMessage);
        }

        private void HandleClientValidationResultMessage(long clientId, string token, JObject data)
        {
            if (CheckAccessToken(clientId, token))
            {
                var validationResult = data.ToObject<RGValidationResult>();
                RGBotServerListener.GetInstance()?.HandleClientValidationResult(clientId, validationResult);
            }
        }

        private void HandleClientRequestMessage(long clientId, string token, JObject data)
        {
            if (CheckAccessToken(clientId, token))
            {
                var actionRequest = data.ToObject<RGActionRequest>();
                RGBotServerListener.GetInstance()?.HandleClientActionRequest(clientId, actionRequest);
            }
        }

        private static bool CheckAccessToken(long clientId, string clientToken)
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

    class ClientConnectionState
    {
        public int socketMessageLength = 0;
        public int socketHeaderBytesReceived = 0;
        public byte[] socketHeaderBytes = new byte[4];
        public string socketState = "header";
        public byte[] socketMessageBytes = new byte[0];
        public int socketMessageBytesReceived = 0;
    }
}
