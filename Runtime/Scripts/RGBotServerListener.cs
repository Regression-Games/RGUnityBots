using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Unity.Plastic.Newtonsoft.Json;
using RegressionGames.RGBotConfigs;
using RegressionGames.StateActionTypes;
using RegressionGames.Types;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RegressionGames
{
    public class RGBotServerListener : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Send a state update every X ticks")]
        public int tickRate = 50;

        private static RGBotServerListener _this = null;
        
        public readonly ConcurrentDictionary<uint, RGAgent> agentMap = new ConcurrentDictionary<uint, RGAgent>();

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
            
            StartServer();
        }

        private TcpListener server = null;


        private bool gameStarted = false;

        private string serverToken = null;

        private readonly ConcurrentDictionary<uint, string> clientTokenMap = new ConcurrentDictionary<uint, string>();
        private readonly ConcurrentDictionary<uint, RGClientConnection> clientConnectionMap = new ConcurrentDictionary<uint, RGClientConnection>();

        // keep these in a map by clientId so that we can do 1 action per client per update call
        private readonly ConcurrentDictionary<uint, ConcurrentQueue<Action>> mainThreadTaskQueue =
            new ConcurrentDictionary<uint, ConcurrentQueue<Action>>();

        public bool IsClientConnected(uint clientId)
        {
            return clientConnectionMap.ContainsKey(clientId);
        }
        
        public void StartServer()
        {
            if (server == null)
            {
                Debug.Log($"Starting Regression Games Bot Server Client Connection Listener");
                IPAddress localAddr = IPAddress.Parse("0.0.0.0");
                server = new TcpListener(localAddr, 19999);
                serverToken = Guid.NewGuid().ToString();
                server.Start();
                ProcessClientConnections();
            }
        }

        /**
         * The server now lasts as long as RG overlay is loaded
         * To stop the 'game' and teardown spawnable players, use StopGame (which this also
         * calls internally).
         */
        public void StopServer()
        {
            if (server != null)
            {
                Debug.Log($"Stopping Regression Games Bot Server Client Connection Listener");
                TcpListener ourServer = server;
                server = null;
                ourServer.Stop();
                StopGame();
                clientConnectionMap.Clear();
                clientTokenMap.Clear();
            }
            serverToken = null;
        }

        public void EndClientConnection(uint clientId)
        {
            if (clientConnectionMap.TryRemove(clientId, out RGClientConnection clientConnection))
            {
                clientConnection.client.Close();
            }
            clientTokenMap.TryRemove(clientId, out _);
            agentMap.TryRemove(clientId, out _);
        }

        /**
         * Only call me on main thread
         *
         * This will teardown the client and despawn the avatar if necessary
         */
        public void TeardownClient(uint clientId)
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
                        RGOverlayMenu.GetInstance()?.UpdateBots();
                    },
                    () => { }
                );
        }

        
        /**
         * Only call me on main thread
         */
        private void StopGameHelper()
        {
            Debug.Log($"Stopping Regression Games Spawnable Bots");
            gameStarted = false;
            
            foreach (uint key in clientConnectionMap.Keys)
            {
                string lifecycle = clientConnectionMap[key].lifecycle;
                if (lifecycle == "MANAGED")
                {
                    // de-spawn and close that client's connection
                    SendToClient(key, "teardown", "{}");
                    TeardownClient(key);
                }
            }
            
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

        private async void StartGameHelper()
        {
            // stop any old stale ones
            StopGameHelper();
            Debug.Log($"Starting Regression Games spawnable Bots");
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            if (rgSettings.GetUseSystemSettings())
            {
                int[] botIds = rgSettings.GetBotsSelected().ToArray();
                int errorCount = 0;
                if (botIds.Length > 0)
                {
                    await Task.WhenAll(botIds.Select(botId => RGServiceManager.GetInstance()?.QueueInstantBot((long)botId, (botInstance) => { }, () => errorCount++)));
                }

                if (errorCount > 0)
                {
                    Debug.LogWarning($"WARNING: Error starting {errorCount} of {botIds.Length} spawnable Regression Games bots, starting without them");
                }
            }

            gameStarted = true;
        }

        public void StartGame()
        {
            enqueueTaskForClient(uint.MaxValue, StartGameHelper);
        }

        private void Update()
        {
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
                        Debug.LogError($"Exception during main thread action processing: {ex.ToString()}");
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
                    foreach (var clientId in clients)
                    {
                        SendToClient(clientId, "tickInfo", data);
                    }

                    Debug.Log($"Sent RG state data from {state.Count} game objects to clients: {string.Join(",", clients)}");
                    //useful, but too spammy
                    //Debug.Log($"TickData: {data}");
                    
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
            foreach (var obj in statefulObjects)
            {
                var state = obj.GetGameObjectState();
                totalState[state["id"].ToString()] = state;
            }

            return totalState;
        }

        public void SpawnBots(bool lateJoin = false)
        {
            enqueueTaskForClient(uint.MaxValue, () =>
            {
                RGBotSpawnManager bsm = RGBotSpawnManager.GetInstance();
                if (bsm != null && gameStarted && server != null)
                {
                    bsm.SpawnBots(lateJoin);
                }
            });
        }

        private Task ProcessClientConnections()
        {
            return Task.Run(() =>
            {
                while (server != null)
                {
                    TcpClient client = server.AcceptTcpClient();
                    Debug.Log($"RG UserCode bot connected");
                    HandleClientConnection(client);
                }
            });
        }

        private Task HandleClientConnection(TcpClient client)
        {
            return Task.Run(() =>
            {

                int socketMessageLength = 0;
                int socketHeaderBytesReceived = 0;
                byte[] socketHeaderBytes = new byte[4];
                string socketState = "header";
                byte[] socketMessageBytes = new byte[0];
                int socketMessageBytesReceived = 0;

                // loop reading data
                while (server != null && client.Connected)
                {
                    byte[] byteBuffer = new byte[1024];
                    NetworkStream socketStream = client.GetStream();
                    int i = socketStream.Read(byteBuffer, 0, byteBuffer.Length);
                    // Debug.Log($"Read {i} bytes from client socket");
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
                                        int headerBytesToCopy = Math.Min(4 - socketHeaderBytesReceived, i - bufferIndex);
                                        Array.Copy(byteBuffer, bufferIndex, socketHeaderBytes, socketHeaderBytesReceived, headerBytesToCopy);
                                        bufferIndex += headerBytesToCopy;
                                        socketHeaderBytesReceived += headerBytesToCopy;
                                    }
                                    if (socketHeaderBytesReceived == 4)
                                    {
                                        socketState = "data";
                                        socketHeaderBytesReceived = 0;
                                        socketMessageLength = BinaryPrimitives.ReadInt32BigEndian(socketHeaderBytes);
                                        socketMessageBytesReceived = 0;
                                        socketMessageBytes = new byte[socketMessageLength];
                                    }
                                    break;
                                case "data":
                                    // copy the data into the message array
                                    int dataBytesToCopy = Math.Min(socketMessageLength - socketMessageBytesReceived, i - bufferIndex);
                                    Array.Copy(byteBuffer, bufferIndex, socketMessageBytes, socketMessageBytesReceived, dataBytesToCopy);

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
                    // TODO: Handle de-pawning their avatar and/or removing them from the lobby?
                }
            });
        }

        public void SendToClient(uint clientId, string type, string data)
        {
            TcpClient client = clientConnectionMap.GetValueOrDefault(clientId, null)?.client;
            try
            {
                if (client != null)
                {
                    string token = clientTokenMap[clientId];
                    RGServerSocketMessage serverSocketMessage = new RGServerSocketMessage(token, type, data);
                    byte[] dataBuffer = Encoding.UTF8.GetBytes(JsonUtility.ToJson(serverSocketMessage));
                    byte[] finalBuffer = new byte[4 + dataBuffer.Length];
                    // put the length header into the buffer first
                    BinaryPrimitives.WriteInt32BigEndian(finalBuffer, dataBuffer.Length);
                    Array.Copy(dataBuffer, 0, finalBuffer, 4, dataBuffer.Length);
                    client.GetStream().WriteAsync(finalBuffer);
                }
            }
            catch (Exception e)
            {
                // on teardown the client can get pulled out from under us
            }
        }

        public void SendHandshakeResponseToClient(uint clientId, string characterConfig, [CanBeNull] string error = null)
        {
            SendToClient(clientId, "handshake",
                JsonUtility.ToJson(new RGServerHandshake(serverToken, characterConfig, error)));
        }

        private void HandleSocketMessage(TcpClient client, string message)
        {
            Debug.Log($"Processing socket message from client, message: {message}");
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
                RGClientHandshake handshakeMessage = JsonUtility.FromJson<RGClientHandshake>(data);

                if (!RGServiceManager.RG_UNITY_AUTH_TOKEN.Equals(handshakeMessage.unityToken))
                {
                    Debug.LogWarning(
                        $"WARNING: A client tried to connect/handshake with an invalid external auth token");
                    return;
                }

                //Handle spawning player and recording lifecycle for de-spawn
                bool spawnable = handshakeMessage.spawnable;

                string lifecycle = string.IsNullOrEmpty(handshakeMessage.lifecycle)
                    ? "MANAGED"
                    : handshakeMessage.lifecycle;

                string botName = handshakeMessage.botName;

                string characterConfig = handshakeMessage.characterConfig;

                // kill existing client if it exists
                if (clientConnectionMap.ContainsKey(clientId))
                {
                    clientConnectionMap[clientId].client?.Close();
                }

                clientConnectionMap[clientId] = new RGClientConnection(lifecycle, client);

                // save the token the client gave us for talking to them
                clientTokenMap[clientId] = handshakeMessage.rgToken;

                // give them the default agent until their player spawns.. thus allowing button clicks
                agentMap[clientId] = this.gameObject.GetComponent<RGAgent>();

                if (spawnable)
                {
                    try
                    {
                        RGBotSpawnManager rgBotSpawnManager = RGBotSpawnManager.GetInstance();
                        if (rgBotSpawnManager != null)
                        {
                            rgBotSpawnManager.SeatPlayer(clientId, characterConfig, botName);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"ERROR seating player - {e.ToString()}");
                    }
                }
                else
                {
                    Debug.Log($"Sending socket handshake response to client id: {clientId}");
                    //send the client a handshake response so they can start processing
                    SendToClient(clientId, "handshake",
                        JsonUtility.ToJson(new RGServerHandshake(serverToken, characterConfig, null)));
                }
            });

        }

        private void HandleClientRequest(TcpClient client, uint clientId, string token, string data)
        {
            // validate token
            if (!token.Equals(serverToken))
            {
                Debug.LogWarning($"WARNING: Client call made with invalid token");
                return;
            }

            enqueueTaskForClient(clientId,() =>
            {
                var actionRequest = JsonConvert.DeserializeObject<RGActionRequest>(data);
                HandleAction(clientId, actionRequest);
            });
            
        }
        
        // call me on the main thread only
        private void HandleAction(uint clientId, RGActionRequest actionRequest)
        {
            var agent = agentMap[clientId];
            RGAction actionHandler = agent.GetActionHandler(actionRequest.action);
            if (actionHandler != null)
            {
                actionHandler.StartAction(actionRequest.input);
            }
        }


    }


}
