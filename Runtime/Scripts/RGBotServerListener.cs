using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using RegressionGames.RGBotConfigs;
using RegressionGames.StateActionTypes;
using RegressionGames.Types;
using UnityEngine;
using UnityEngine.SceneManagement;
using TcpClient = System.Net.Sockets.TcpClient;

namespace RegressionGames
{
    public class RGBotServerListener : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Send a state update every X ticks")]
        public int tickRate = 50;

        private static RGBotServerListener _this = null;
        
        public readonly ConcurrentDictionary<uint?, HashSet<RGAgent>> agentMap = new ();

        private long tick = 0;

        public static RGBotServerListener GetInstance()
        {
            return _this;
        }

        public void Awake()
        {
            // only allow 1 of these to be alive
            if( _this != null && _this.gameObject != this.gameObject)
            {
                Destroy(this.gameObject);
                return;
            }
            // keep this thing alive across scenes
            DontDestroyOnLoad(this.gameObject);
            _this = this;
        }

        void OnApplicationQuit()
        {
            StopBotClientConnections();
        }

        private bool gameStarted = false;

        private string unitySideToken = Guid.NewGuid().ToString();
        
        private readonly ConcurrentDictionary<uint?, string> clientTokenMap = new ();

        /*
         * Tracking Maps
         * 
         * clientConnectionMap - Clients that have connected and/or done their handshake will be populated here.
         *    Clients waiting on a connection will have a null value.
         *    (key is the botInstanceId as a uint)
         *
         *
         * Connection process
         * 1. RGServiceManager.QueueInstantBot sends a request to RGService to queue the instant bot
         *    - In the 'background' this calls through to GIS and UCS and starts a NodeJS bot runtime
         *      on some unknown game machine/port.
         *
         * 2. On Success of RGServiceManager.QueueInstantBot, that botInstanceId is added to a connections
         *    list in RGBotServerListener (this class).
         * 
         *    On Unity Update, we evaluate the set of botInstanceIds that don't have a connection yet.
         *    For each of these, we attempt to use RGServiceManager.GetExternalConnectionInformationForBotInstance
         *    to call RGService to get the external connection information for the bot address:port.
         *    If this information is available, we initiate a connection.
         *
         * 3. On FixedUpdate, when we send tickInfo, if any of the sends fail, we recycle their bot connection assuming
         *    that the bot code was reloaded/restarted.
         */
        [ItemCanBeNull] private readonly ConcurrentDictionary<uint, RGClientConnection> clientConnectionMap = new ();
        
        [ItemCanBeNull] private readonly ConcurrentDictionary<uint?, ConcurrentQueue<RGValidationResult>> clientValidationMap = new ();
 
        // keep these in a map by clientId so that we can do 1 action per client per update call
        private readonly ConcurrentDictionary<uint, ConcurrentQueue<Action>> mainThreadTaskQueue =
            new ConcurrentDictionary<uint, ConcurrentQueue<Action>>();

        public bool IsClientConnected(uint clientId)
        {
            if (clientConnectionMap.ContainsKey(clientId))
            {
                RGClientConnection conn = clientConnectionMap[clientId];
                if (conn != null)
                {
                    return conn.Connected;
                }
            }

            return false;
        }

        public void AddClientConnectionForBotInstance(long botInstanceId)
        {
            RGDebug.LogDebug($"Adding Client Connection Entry from botInstanceId: {botInstanceId}");
            // set or update the connection info for this botInstanceId
            clientConnectionMap.AddOrUpdate((uint)botInstanceId, new RGClientConnection((uint)botInstanceId), (k,v) =>
            {
                return v;
            });
            clientValidationMap.AddOrUpdate((uint)botInstanceId, new ConcurrentQueue<RGValidationResult>(), (k, v) => {
                 return v;
                });
        }

        /**
         * Returns true if there are bots connected and running within this game.
         */
        public bool HasBotsRunning()
        {
            return !clientConnectionMap.IsEmpty;
        }

        public ConcurrentQueue<RGValidationResult> GetFailedValidationsForClient(uint clientId)
        {
            if (clientValidationMap.TryGetValue(clientId, out var validations))
            {
                return validations;
            }
            return new ConcurrentQueue<RGValidationResult>();
        }

        /**
         * The server now lasts as long as RG overlay is loaded
         * To stop the 'game' and teardown spawnable players, use StopGame (which this also
         * calls internally).
         * ONLY CALL THIS ON THE MAIN THREAD
         */
        public void StopBotClientConnections()
        {
            RGDebug.LogInfo($"Stopping Bot Client Connections");
            StopGameHelper();
            EndAllClientConnections();
            clientConnectionMap.Clear();
            clientValidationMap.Clear();
            clientTokenMap.Clear();
            
            unitySideToken = Guid.NewGuid().ToString();
        }

        public void EndAllClientConnections()
        {
            List<uint> clientIds = clientConnectionMap.Keys.ToList();
            foreach (var clientId in clientIds)
            {
                EndClientConnection(clientId);
            }
        }

        public void EndClientConnection(uint clientId)
        {
            if (clientConnectionMap.TryRemove(clientId, out RGClientConnection clientConnection))
            {
                clientConnection.client?.Close();
            }
            // Don't do this here, we only remove the validation on StopGame so the results are available to test cases
            //clientValidationMap.TryRemove(clientId, out _);
            clientTokenMap.TryRemove(clientId, out _);
            agentMap.TryRemove(clientId, out _);
        }

        /**
         * Only call me on main thread
         *
         * This will teardown the client and despawn the avatar if necessary
         */
        public void TeardownClient(uint clientId, bool doUpdateBots=true)
        {
            // do this before we end the connection so the player is still in the map
            RGBotSpawnManager.GetInstance()?.TeardownBot(clientId);

            EndClientConnection(clientId);
            
            // we originally didn't call RGService StopBotInstance here
            // But.. leaving the bot running was annoying in the editor to have to stop it
            // every time.  In the future, we need to solve how to easily get to the bot replay
            // data for stopped bots.
            _ = RGServiceManager.GetInstance()?.StopBotInstance(
                    clientId,
                    () =>
                    {
                        if (doUpdateBots)
                        {
                            RGOverlayMenu.GetInstance()?.UpdateBots();
                        }
                    },
                    () =>
                    {
                        if (doUpdateBots)
                        {
                            RGOverlayMenu.GetInstance()?.UpdateBots();
                        }
                    }
                );
        }

        
        /**
         * Only call me on main thread
         */
        private void StopGameHelper()
        {
            RGDebug.LogInfo($"Stopping Spawnable Bots");
            gameStarted = false;
            
            foreach (var keyvalue in clientConnectionMap)
            {
                string lifecycle = keyvalue.Value.lifecycle;
                if ("MANAGED" == lifecycle)
                {
                    // de-spawn and close that client's connection
                    SendToClient(keyvalue.Key, "teardown", "{}");
                    TeardownClient(keyvalue.Key, false);
                }
            }
            
            // update the bot list one time after tearing them down instead of on every one
            RGOverlayMenu.GetInstance()?.UpdateBots();
            
            RGBotSpawnManager rgBotSpawnManager = RGBotSpawnManager.GetInstance();
            if (rgBotSpawnManager != null)
            {
                rgBotSpawnManager.StopGame();
            }
        }

        public void StopGame()
        {
            // shutdown clients and de-spawn players that should be de-spawned
            enqueueTaskForClient(uint.MaxValue, StopGameHelper);
        }

        private void StartGameHelper()
        {
            // stop any old stale ones
            StopGameHelper();
            RGDebug.LogInfo($"Starting Spawnable Bots");
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            if (rgSettings.GetUseSystemSettings())
            {
                int[] botIds = rgSettings.GetBotsSelected().ToArray();
                int errorCount = 0;
                if (botIds.Length > 0)
                {
                    // don't await here to avoid this method being defined async, which
                    // would cause big issues as the main thread Update runner wouldn't await it and
                    // gameStarted wouldn't reliably get set before they called SpawnBots
                    Task.WhenAll(botIds.Select(botId => RGServiceManager.GetInstance()?.QueueInstantBot(
                                (long)botId,
                                async (botInstance) => { AddClientConnectionForBotInstance(botInstance.id); },
                                () => { RGDebug.LogWarning($"WARNING: Error starting botId: {botId}, starting without them"); }
                                )
                            )
                        );
                }
            }

            gameStarted = true;
        }

        public void StartGame()
        {
            enqueueTaskForClient(uint.MaxValue, StartGameHelper);
        }
        
        private async Task SetupClientConnection(RGClientConnection clientConnection)
        {
            if (clientConnection.client == null && !clientConnection.connecting)
            {
                // MUST do this on the main thread
                // update to the latest connection info from RGService
                RGDebug.LogDebug($"Getting external connection information for botInstanceId: {clientConnection.clientId}");
                await RGServiceManager.GetInstance()?.GetExternalConnectionInformationForBotInstance(
                    (long)clientConnection.clientId,
                    (connInfo) =>
                    {
                        clientConnection.connectionInfo = connInfo;
                    },
                    () =>
                    {
                        clientConnection.connecting = false;
                    }
                );
                await Task.Run(() =>
                {
                    // make sure we only setup 1 connection at a time on this connection object
                    lock (clientConnection)
                    {
                        if (clientConnection.client == null && clientConnection.connectionInfo != null)
                        {
                            clientConnection.handshakeComplete = false;
                            // make sure we were able to get the current connection info
                            if (clientConnection.connectionInfo != null)
                            {
                                TcpClient client = new TcpClient();
                                // create a new TcpClient, then start a connect attempt asynchronously
                                string address = clientConnection.connectionInfo.address;
                                int port = clientConnection.connectionInfo.port;

                                clientConnection.client = client;
                                clientConnection.connecting = true;
                                RGDebug.LogInfo($"Connecting to bot at {address}:{port} for ClientId: {clientConnection.clientId}");
                                IAsyncResult beginConnect = client.BeginConnect(address, port, ar =>
                                {
                                    if (clientConnection.connecting)
                                    {
                                        lock (clientConnection)
                                        {
                                            if (clientConnection.connecting)
                                            {
                                                clientConnection.connecting = false;
                                                // nodejs side should start handshakes/etc
                                                // we just need to save our connection reference
                                                try
                                                {
                                                    client.EndConnect(ar);
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

                                                    // failed to connect, clear out the client on the connection for this botInstance so it can re-connect
                                                    clientConnection.client = null;
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
                                    if (clientConnection.connecting)
                                    {
                                        lock (clientConnection)
                                        {
                                            if (clientConnection.connecting)
                                            {
                                                RGDebug.LogInfo(
                                                    $"Connection TIMED OUT to bot at {address}:{port} for ClientId: {clientConnection.clientId}");
                                                clientConnection.connecting = false;
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

                                                // failed to connect, clear out the client on the connection for this botInstance so it can re-connect
                                                clientConnection.client = null;
                                            }
                                        }
                                    }
                                };
                                t.AutoReset = false;
                                t.Start();
                            }
                            else
                            {
                                clientConnection.connecting = false;
                            }
                        }
                    }
                });
            }
        }

        private float lastClientConnectTime = -1f;

        private void Update()
        {
            // only evaluate re-connecting once per second
            float time = Time.time;
            if (time - lastClientConnectTime >= 1f)
            {
                foreach (var keyvalue in clientConnectionMap)
                {
                    if (keyvalue.Value.client == null)
                    {
                        RGDebug.LogVerbose($"Update needs to connect client for botInstanceId: {keyvalue.Key}");
                        _ = SetupClientConnection(keyvalue.Value);
                    }
                }

                lastClientConnectTime = time;
            }

            if (gameStarted)
            {
                SpawnBots(true);
            }

            // for each client, run up to 1 action per update
            foreach (uint key in mainThreadTaskQueue.Keys)
            {
                if (mainThreadTaskQueue[key].TryDequeue(out Action action) )
                {
                    try
                    {
                        action.Invoke();
                    }
                    catch (Exception ex)
                    {
                        RGDebug.LogError($"Exception during main thread action processing: {ex.ToString()}");
                    }
                }
            }
        }

        /**
         * When all game actions occur, finally decide whether or not to send the game state
         */
        private void FixedUpdate()
        {
            
            tick++;
            if (tick % tickRate == 0)
            {
                ICollection<uint> clients = clientConnectionMap.Keys;
                if (clients.Count > 0)
                {
                    var state = GetGameState();
                    var sceneName = SceneManager.GetActiveScene().name;
                    var tickInfoData = new RGTickInfoData(tick, sceneName, state);
                    var data = JsonConvert.SerializeObject(tickInfoData, Formatting.Indented, new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    });
                    List<uint> sentTo = new List<uint>();
                    foreach (var clientId in clients)
                    {
                        if(SendToClient(clientId, "tickInfo", data)) 
                        {
                            sentTo.Add(clientId);
                        }
                    }

                    if (sentTo.Count > 0)
                    {
                        RGDebug.LogDebug(
                            $"Sent RG state data from {state.Count} game objects to clients: {string.Join(",", sentTo)}");
                        //useful, but too spammy
                        RGDebug.LogVerbose($"TickData: {data}");
                    }
                    else
                    {
                        // Useful for debugging threading / callstack issues
                        //Debug.Log($"Skipping RG state data update, no clients connected");
                    }
                }
                else
                {
                    // Useful for debugging threading / callstack issues
                    //Debug.Log($"Skipping RG state data update, no clients connected");
                }
            }
        }

        /**
         * Gets the entire game state by searching for all RGState game objects and gather their
         * states.
         *
         * TODO: Organize in hierarchy of parent-child rather than a flat map
         */
        private Dictionary<string, object> GetGameState()
        {
            var statefulObjects = FindObjectsOfType<RGState>();
            var totalState = new Dictionary<string, object>();
            foreach (var rgState in statefulObjects)
            {
                var state = rgState.GetGameObjectState();
                // if this object is a 'player' ... put the clientId that owns it into the state
                if (rgState.isPlayer)
                {
                    var rgAgent = rgState.GetComponentInParent<RGAgent>();
                    if (rgAgent != null)
                    {
                        var clientId = agentMap.FirstOrDefault(x => x.Value.Contains(rgAgent)).Key;
                        if (clientId != null)
                        {
                            state["clientId"] = clientId;
                        }
                    }

                    if (!state.ContainsKey("clientId"))
                    {
                        // for things like menu bots that end up spawning a human player
                        // use the agent from the overlay
                        // Note: We have to be very careful here or we'll set this up wrong
                        // we only want to give the overlay agent to the human player.
                        // Before the clientIds are all connected, this can mess-up
                        var overlayAgent = this.gameObject.GetComponent<RGAgent>();
                        var clientId = agentMap.FirstOrDefault(x => x.Value.Contains(overlayAgent)).Key;
                        if (clientId != null)
                        {
                            state["clientId"] = clientId;
                            // add the agent from the player's object to the agentMap now that 
                            // we have detected that they are here 
                            // this happens for menu bots that spawn human players to control
                            // doing this allows actions from the bot code to process to the human player agent
                            if (rgAgent != null)
                            {
                                agentMap[clientId].Add(rgAgent);
                            }
                        }
                    }
                }
                totalState[state["id"].ToString()] = state;
            }
            return totalState;
        }

        public void SpawnBots(bool lateJoin = false)
        {
            enqueueTaskForClient(uint.MaxValue, () =>
            {
                RGBotSpawnManager bsm = RGBotSpawnManager.GetInstance();
                if (bsm != null && gameStarted)
                {
                    bsm.SpawnBots(lateJoin);
                }
            });
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
                                        HandleSocketMessage(client, sockMessage);
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

        public bool SendToClient(uint clientId, string type, string data)
        {
            RGClientConnection? clientConnection = clientConnectionMap.GetValueOrDefault(clientId, null);
            if (clientConnection != null )
            {
                try
                {
                    if (clientConnection.client != null && clientConnection.handshakeComplete)
                    {
                        string token = clientTokenMap[clientId];
                        RGServerSocketMessage serverSocketMessage =
                            new RGServerSocketMessage(token, type, data);
                        byte[] dataBuffer = Encoding.UTF8.GetBytes(JsonUtility.ToJson(serverSocketMessage));
                        byte[] finalBuffer = new byte[4 + dataBuffer.Length];
                        // put the length header into the buffer first
                        BinaryPrimitives.WriteInt32BigEndian(finalBuffer, dataBuffer.Length);
                        Array.Copy(dataBuffer, 0, finalBuffer, 4, dataBuffer.Length);
                        ValueTask vt = clientConnection.client.GetStream().WriteAsync(finalBuffer);
                        vt.ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
                        {
                            if (!vt.IsCompletedSuccessfully)
                            {
                                RGDebug.LogDebug($"Client Id: {clientId} socket error or closed, need to re-establish connection for bot");
                                // client got pulled out from under us or restarted/reloaded.. handle it on the next Update
                                try
                                {
                                    clientConnection.client?.Close();
                                }
                                catch (Exception ex)
                                {
                                }

                                // we could despawn their avatar, but not remove them from the lobby
                                // however we don't do this as we lose the avatar position/playerclass to
                                // restore on re-connect
                                //RGBotSpawnManager.GetInstance()?.DeSpawnBot(clientId);
                                
                                clientConnection.client = null;
                            }
                        });
                        return true;
                    }
                }
                catch (Exception e)
                {
                    RGDebug.LogInfo($"Client Id: {clientId} socket error or closed, need to re-establish connection for bot");
                    // client got pulled out from under us or restarted/reloaded.. handle it on the next Update
                    try
                    {
                        clientConnection.client?.Close();
                    }
                    catch (Exception ex)
                    {
                    }
                    
                    // we could despawn their avatar, but not remove them from the lobby
                    // however we don't do this as we lose the avatar position/playerclass to
                    // restore on re-connect
                    //RGBotSpawnManager.GetInstance()?.DeSpawnBot(clientId);
                    
                    clientConnection.client = null;
                }
            }

            return false;
        }

        public void SendHandshakeResponseToClient(uint clientId, string characterConfig, [CanBeNull] string error = null)
        {
            SendToClient(clientId, "handshake",
                    JsonUtility.ToJson(new RGServerHandshake(unitySideToken, characterConfig, error)));
        }

        private void HandleSocketMessage(TcpClient client, string message)
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
                    HandleClientHandshake(client, clientId, data);
                    break;
                case "validationResult":
                    HandleClientValidationResult(client, clientId, token, data);
                    break;
                case "request":
                    HandleClientRequest(client, clientId, token, data);
                    break;
                case "teardown":
                    HandleClientTeardown(client, clientId, data);
                    break;
            }


        }

        private void enqueueTaskForClient(uint clientId, Action task)
        {
            mainThreadTaskQueue.TryAdd(clientId, new ConcurrentQueue<Action>());
            mainThreadTaskQueue[clientId].Enqueue(task);
            
        }

        private void HandleClientTeardown(TcpClient client, uint clientId, string data)
        {
            // Handle when the client tells us to teardown because the instant bot instance was stopped
            // This would happen when a particular bot's code determined it was finished and sent
            // us a teardown notification
            enqueueTaskForClient(clientId, () =>
            {
                TeardownClient(clientId);
            });
        }
        
        // TODO: Handle client disconnects that aren't clean teardown requests ??

        private void HandleClientHandshake(TcpClient client, uint clientId, string data)
        {
            // can only call Unity APIs on main thread, so queue this up
            enqueueTaskForClient(clientId,() =>
            {
                try
                {
                    RGClientHandshake handshakeMessage = JsonUtility.FromJson<RGClientHandshake>(data);


                    if (!RGServiceManager.RG_UNITY_AUTH_TOKEN.Equals(handshakeMessage.unityToken))
                    {
                        RGDebug.LogWarning(
                            $"WARNING: A client tried to connect/handshake with an invalid external auth token");
                        return;
                    }
                    
                    //Handle spawning player and recording lifecycle for de-spawn
                    bool spawnable = handshakeMessage.spawnable;

                    string lifecycle = string.IsNullOrEmpty(handshakeMessage.lifecycle)
                        ? "MANAGED"
                        : handshakeMessage.lifecycle;
                    
                    clientConnectionMap[clientId].lifecycle = lifecycle;

                    string botName = handshakeMessage.botName;
                    string characterConfig = handshakeMessage.characterConfig;

                    // save the token the client gave us for talking to them
                    clientTokenMap[clientId] = handshakeMessage.rgToken;

                    if (!spawnable && "PERSISTENT".Equals(lifecycle))
                    {
                        // should be a menu / human simulator bot, give them the default agent... thus allowing button clicks
                        agentMap[clientId] = new HashSet<RGAgent> { this.gameObject.GetComponent<RGAgent>() };
                    }
                    else
                    {
                        agentMap[clientId] = new HashSet<RGAgent>( );
                    }

                    // set this BEFORE sending the response of handshake to the client so it actually sends
                    clientConnectionMap[clientId].handshakeComplete = true;
                    if (spawnable)
                    {
                        try
                        {
                            RGBotSpawnManager.GetInstance()?.CallSeatBot(new BotInformation(clientId, botName, characterConfig));
                        }
                        catch (Exception e)
                        {
                            RGDebug.LogError($"ERROR seating player - {e}");
                        }
                    }
                    else
                    {
                        RGDebug.LogDebug($"Sending socket handshake response to client id: {clientId}");
                        //send the client a handshake response so they can start processing
                        SendHandshakeResponseToClient(clientId, characterConfig, null);
                    }
                }
                catch (Exception ex)
                {
                    RGDebug.LogWarning($"WARNING: Failed to process handshake from clientId: {clientId} - {ex}");
                }
            });

        }
        
        private void HandleClientValidationResult(TcpClient client, uint clientId, string token, string data)
        {
            // validate token
            if (!token.Equals(unitySideToken))
            {
                RGDebug.LogWarning($"WARNING: Client call made with invalid token");
                return;
            }

            enqueueTaskForClient(clientId,() =>
            {
                var validationResult = JsonConvert.DeserializeObject<RGValidationResult>(data);
                if (!validationResult.passed)
                {
                    RGDebug.LogDebug($"Save Failed Validation Result for clientId: {clientId}, data: {data}");
                    clientValidationMap[(uint)clientId]?.Enqueue(validationResult);
                }
            });
            
        }

        private void HandleClientRequest(TcpClient client, uint clientId, string token, string data)
        {
            // validate token
            if (!token.Equals(unitySideToken))
            {
                RGDebug.LogWarning($"WARNING: Client call made with invalid token");
                return;
            }

            enqueueTaskForClient(clientId,() =>
            {
                RGDebug.LogDebug($"QUEUE TASK for clientId: {clientId}, data: {data}");
                var actionRequest = JsonConvert.DeserializeObject<RGActionRequest>(data);
                HandleAction(clientId, actionRequest);
            });
            
        }
        
        // call me on the main thread only
        private void HandleAction(uint clientId, RGActionRequest actionRequest)
        {
            var agents = agentMap[clientId];
            /* TODO: Right now this broadcasts the action to all agents.  We may
             * want to assign actions to specific agents based on their entity Id
             * if we ever support more than 1 entity + button clicks
             */
            foreach (var agent in agents)
            {
                RGAction actionHandler = agent.GetActionHandler(actionRequest.action);
                if (actionHandler != null)
                {
                    actionHandler.StartAction(actionRequest.input);
                }
            }
        }


    }


}
