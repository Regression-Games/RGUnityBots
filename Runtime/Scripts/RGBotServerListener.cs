using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
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

        public readonly ConcurrentDictionary<uint?, HashSet<RGAgent>> agentMap = new ();

        private long tick = 0;

        private static RGBotServerListener _this = null;
        
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

        public string UnitySideToken { get; private set; }= Guid.NewGuid().ToString();

        private readonly ConcurrentDictionary<uint, RGUnityBotState> botStates = new();

        private readonly ConcurrentDictionary<uint, List<Action<RGUnityBotState>>> botStateListeners = new();

        public void AddUnityBotStateListener(uint id, Action<RGUnityBotState> func)
        {
            botStateListeners.AddOrUpdate(id, new List<Action<RGUnityBotState>> {func}, (key, oldValue) =>
            {
                oldValue.Add(func);
                return oldValue;
            });
        }

        public RGUnityBotState GetUnityBotState(uint id)
        {
            if (botStates.TryGetValue(id, out RGUnityBotState state))
            {
                return state;
            }
            return RGUnityBotState.UNKNOWN;
        }
        
        public void SetUnityBotState(uint id, RGUnityBotState state)
        {
            botStates.AddOrUpdate(id, (newValue) =>
            {
                SendStateUpdatesToListeners(id, state);
                return state;
            }, (key, oldValue) =>
            {
                if (!oldValue.Equals(state))
                {
                    SendStateUpdatesToListeners(id, state);
                    RGDebug.LogInfo($"State of Bot id: {id} == {state}");        
                }
                return state;
            });
        }

        private void SendStateUpdatesToListeners(uint id, RGUnityBotState newState)
        {
            if (botStateListeners.TryGetValue(id, out List<Action<RGUnityBotState>> funcs))
            {
                foreach (var action in funcs)
                {
                    try
                    {
                        action.Invoke(newState);
                    }
                    catch (Exception ex)
                    {
                        RGDebug.LogWarning($"Exception calling action listener for state update on bot id: {id} - {ex}");
                    }
                }
            }
        }

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
        private readonly ConcurrentDictionary<uint, ConcurrentQueue<Action>> mainThreadTaskQueue = new ();

        public RGClientConnection GetClientConnection(uint clientId)
        {
            if (clientConnectionMap.TryGetValue(clientId, out RGClientConnection result))
            {
                return result;
            }
            return null;
        }

        public RGClientConnection AddClientConnectionForBotInstance(long botInstanceId, RGClientConnectionType type)
        {
            RGDebug.LogDebug($"Adding Client Connection Entry from botInstanceId: {botInstanceId}");
            // set or update the connection info for this botInstanceId
            RGClientConnection connection;
            if (type == RGClientConnectionType.REMOTE)
            {
                connection = new RGClientConnection_Remote(clientId: (uint)botInstanceId);
            }
            else
            {
                connection = new RGClientConnection_Local(clientId: (uint)botInstanceId);
            }
            
            clientConnectionMap.AddOrUpdate((uint)botInstanceId, connection, (k,v) =>
            {
                return v;
            });
            clientValidationMap.AddOrUpdate((uint)botInstanceId, new ConcurrentQueue<RGValidationResult>(), (k, v) => 
            {
                return v;
            });

            return connection;
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
            botStateListeners.Clear();
            botStates.Clear();
            mainThreadTaskQueue.Clear();
            
            UnitySideToken = Guid.NewGuid().ToString();
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
                clientConnection.Close();
            }
            // Don't do this here, we only remove the validation on StopGame so the results are available to test cases
            //clientValidationMap.TryRemove(clientId, out _);
            agentMap.TryRemove(clientId, out _);
            botStateListeners.TryRemove(clientId, out _);
            botStates.TryRemove(clientId, out _);
            mainThreadTaskQueue.TryRemove(clientId, out _);
        }

        /**
         * Only call me on main thread
         *
         * This will teardown the client and de-spawn the avatar if necessary
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
            // TODO: Don't call this for local bots ...
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
                string lifecycle = keyvalue.Value.Lifecycle;
                if ("MANAGED" == lifecycle)
                {
                    // de-spawn and close that client's connection
                    keyvalue.Value.SendTeardown();
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
                //TODO: Local Bots... This needs to support Local Bots, not just remote
                int[] botIds = rgSettings.GetBotsSelected().ToArray();
                int errorCount = 0;
                if (botIds.Length > 0)
                {
                    // don't await here to avoid this method being defined async, which
                    // would cause big issues as the main thread Update runner wouldn't await it and
                    // gameStarted wouldn't reliably get set before they called SpawnBots
                    Task.WhenAll(botIds.Select(botId => RGServiceManager.GetInstance()?.QueueInstantBot(
                                (long)botId,
                                async (botInstance) => { AddClientConnectionForBotInstance(botInstance.id, RGClientConnectionType.REMOTE); },
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
            // MUST do this on the main thread
            // update to the latest connection info from RGService
            clientConnection.Connect();
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
                    if (!keyvalue.Value.Connected())
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
                    List<uint> sentTo = new List<uint>();
                    foreach (var clientId in clients)
                    {
                        if(clientConnectionMap[clientId].SendTickInfo(tickInfoData)) 
                        {
                            sentTo.Add(clientId);
                        }
                    }

                    if (sentTo.Count > 0)
                    {
                        RGDebug.LogDebug(
                            $"Sent RG state data from {state.Count} game objects to clients: {string.Join(",", sentTo)}");
                        //useful, but too spammy
                        RGDebug.LogVerbose($"TickData: {tickInfoData}");
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
        private Dictionary<string, RGStateEntity> GetGameState()
        {
            var statefulObjects = FindObjectsOfType<RGState>();
            var totalState = new Dictionary<string, RGStateEntity>();
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

        private void enqueueTaskForClient(uint clientId, Action task)
        {
            mainThreadTaskQueue.TryAdd(clientId, new ConcurrentQueue<Action>());
            mainThreadTaskQueue[clientId].Enqueue(task);
        }
        
        public void HandleClientTeardown(uint clientId)
        {
            // Handle when the client tells us to teardown because the instant bot instance was stopped
            // This would happen when a particular bot's code determined it was finished and sent
            // us a teardown notification
            SetUnityBotState(clientId, RGUnityBotState.TEARING_DOWN);
            enqueueTaskForClient(clientId, () =>
            {
                TeardownClient(clientId);
            });
        }

        public void HandleClientHandshakeMessage(uint clientId, RGClientHandshake handshakeMessage)
        {
            // can only call Unity APIs on main thread, so queue this up
            enqueueTaskForClient(clientId,() =>
            {
                try
                {
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
                    
                    clientConnectionMap[clientId].Lifecycle = lifecycle;

                    // if the bot coming in already put its unique client Id on the end.. ignore
                    // else make sure the name is unique by appending the id
                    string botName = $"{handshakeMessage.botName}";
                    string clientIdStringSuffix = $"-{clientId}";
                    if (!botName.EndsWith(clientIdStringSuffix))
                    {
                        botName = botName + clientIdStringSuffix;
                    }
                    string characterConfig = handshakeMessage.characterConfig;

                    // save the token the client gave us for talking to them
                    clientConnectionMap[clientId].Token = handshakeMessage.rgToken;

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
                    SetUnityBotState(clientId, RGUnityBotState.CONNECTED);
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
                        clientConnectionMap[clientId].SendHandshakeResponse( new RGServerHandshake(UnitySideToken, characterConfig, null));
                    }
                }
                catch (Exception ex)
                {
                    RGDebug.LogWarning($"WARNING: Failed to process handshake from clientId: {clientId} - {ex}");
                }
            });

        }
        


        public void HandleClientValidationResult(uint clientId, RGValidationResult validationResult)
        {
            enqueueTaskForClient(clientId,() =>
            {
                if (!validationResult.passed)
                {
                    RGDebug.LogDebug($"Save Failed Validation Result for clientId: {clientId}, data: {validationResult}");
                    clientValidationMap[(uint)clientId]?.Enqueue(validationResult);
                }
            });
        }

        public void HandleClientActionRequest(uint clientId, RGActionRequest actionRequest)
        {
            enqueueTaskForClient(clientId,() =>
            {
                RGDebug.LogDebug($"QUEUE TASK for clientId: {clientId}, data: {actionRequest}");
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
                    actionHandler.StartAction(actionRequest.Input);
                }
            }
        }


    }


}
