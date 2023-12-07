using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using RegressionGames.DataCollection;
using RegressionGames.RGBotConfigs;
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.StateActionTypes;
using RegressionGames.Types;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RegressionGames
{
    [HelpURL("https://docs.regression.gg/studios/unity/unity-sdk/overview")]
    public class RGBotServerListener : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Send a state update every X ticks")]
        public int tickRate = 50;

        public readonly ConcurrentDictionary<long?, HashSet<RGEntity>> agentMap = new ();

        private long tick = 0;

        private static RGBotServerListener _this = null;

        private RGDataCollection _dataCollection = null;

        /**
         * Names of fields that are allowed to appear in multiple state scripts for the same GameObject.
         * These are typically fields whose values are inherited from the GameObject's RGEntity component,
         * and are expected to have the same value for each IRGState script attached to the GameObject.
         */
        private List<string> _duplicatedStateFields = new();

        // of the core state fields, only position and rotation can be overridden by custom state classes
        // an example of this being useful is remapping position for a platformer character to better align with
        // their collider's bottom edge instead of the center of the character
        private HashSet<string> _overridableCoreStateFields = new()
        {
            "position",
            "rotation"
        };
        
        public static RGBotServerListener GetInstance()
        {
            return _this;
        }

        public void Start()
        {
            _dataCollection = new RGDataCollection();
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
            StopBotClientConnections(false);
        }

        private bool gameStarted = false;

        public string UnitySideToken { get; private set; }= Guid.NewGuid().ToString();

        private readonly ConcurrentDictionary<long, RGUnityBotState> botStates = new();

        private readonly ConcurrentDictionary<long, List<Action<RGUnityBotState>>> botStateListeners = new();

        public void AddUnityBotStateListener(long id, Action<RGUnityBotState> func)
        {
            botStateListeners.AddOrUpdate(id, new List<Action<RGUnityBotState>> {func}, (key, oldValue) =>
            {
                oldValue.Add(func);
                return oldValue;
            });
        }

        public RGUnityBotState GetUnityBotState(long id)
        {
            if (botStates.TryGetValue(id, out RGUnityBotState state))
            {
                return state;
            }
            return RGUnityBotState.UNKNOWN;
        }
        
        public void SetUnityBotState(long id, RGUnityBotState state)
        {
            if (botStates.TryGetValue(id, out var oldValue))
            {
                if (!oldValue.Equals(state))
                {
                    botStates[id] = state;
                    SendStateUpdatesToListeners(id, state);
                }
            }
            else
            {
                botStates[id] = state;
                SendStateUpdatesToListeners(id, state);
            }
        }

        private void SendStateUpdatesToListeners(long id, RGUnityBotState newState)
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
         *    (key is the botInstanceId as a long)
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
        [ItemCanBeNull] private readonly ConcurrentDictionary<long, RGClientConnection> clientConnectionMap = new ();
        
        [ItemCanBeNull] private readonly ConcurrentDictionary<long?, ConcurrentQueue<RGValidationResult>> clientValidationMap = new ();
 
        // keep these in a map by clientId so that we can do 1 action per client per update call
        private readonly ConcurrentDictionary<long, ConcurrentQueue<Action>> mainThreadTaskQueue = new ();

        public RGClientConnection GetClientConnection(long clientId)
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
                connection = new RGClientConnection_Remote(clientId: botInstanceId);
            }
            else
            {
                connection = new RGClientConnection_Local(clientId: botInstanceId);
            }
            
            clientConnectionMap.AddOrUpdate(botInstanceId, connection, (k,v) =>
            {
                return v;
            });
            clientValidationMap.AddOrUpdate(botInstanceId, new ConcurrentQueue<RGValidationResult>(), (k, v) => 
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

        public ConcurrentQueue<RGValidationResult> GetFailedValidationsForClient(long clientId)
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
        public void StopBotClientConnections(bool updateBotsList = true)
        {
            RGDebug.LogInfo($"Stopping Bot Client Connections");
            StopGameHelper(updateBotsList);
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
            List<long> clientIds = clientConnectionMap.Keys.ToList();
            foreach (var clientId in clientIds)
            {
                EndClientConnection(clientId);
            }
        }

        public void EndClientConnection(long clientId)
        {
            if (clientConnectionMap.TryRemove(clientId, out var clientConnection))
            {
                clientConnection.SendTeardown();
                
                clientConnection.Close();
                
                // Upload the replay data for this bot
                if (clientConnection.Type == RGClientConnectionType.LOCAL)
                {
                    // Kick off saving the bot work
                    // TODO: (REG-1422) we DO NOT wait for this.. we will deal with interrupted during shutdown later
                    _ = _dataCollection.SaveBotInstanceHistory(clientId);
 
                }
                
                // we originally didn't call RGService StopBotInstance here
                // But.. leaving the bot running was annoying in the editor to have to stop it
                // every time.  In the future, we need to solve how to easily get to the bot replay
                // data for stopped bots.
                // --- Don't call this for local bots ...
                if (clientConnection.Type == RGClientConnectionType.REMOTE)
                {
                    _ = RGServiceManager.GetInstance()?.StopBotInstance(
                        clientId,
                        () => { },
                        () => { }
                    );
                }
            }
            else
            {
                // we didn't have this bot... its not ours to know about
                SetUnityBotState(clientId, RGUnityBotState.UNKNOWN);
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
        private void TeardownClient(long clientId, bool doUpdateBots=true)
        {
            // do this before we end the connection so the player is still in the map
            RGBotSpawnManager.GetInstance()?.TeardownBot(clientId);

            EndClientConnection(clientId);

            if (doUpdateBots)
            {
                RGOverlayMenu.GetInstance()?.UpdateBots();
            }
        }

        /**
         * Only call me on the main thread
         *
         * WARNING: This will teardown ALL local and remote clients and de-spawn their avatars if necessary
         */
        public void TeardownAllClients()
        {
            foreach (var (clientId, rgClientConnection) in clientConnectionMap)
            {
                SetUnityBotState(clientId, RGUnityBotState.TEARING_DOWN);
            }

            enqueueTaskForClient(0, () =>
            {
                // do these all together on a single main thread update
                foreach (var (clientId, rgClientConnection) in clientConnectionMap)
                {
                    TeardownClient(clientId, false);
                }
                // do this once after all the teardowns
                RGOverlayMenu.GetInstance()?.UpdateBots();
            });
            
        }

        
        /**
         * Only call me on main thread
         */
        private void StopGameHelper(bool updateBotsList = true)
        {
            RGDebug.LogInfo($"Stopping Spawnable Bots");
            gameStarted = false;
            
            foreach (var keyvalue in clientConnectionMap)
            {
                string lifecycle = keyvalue.Value.Lifecycle;
                if ("MANAGED" == lifecycle)
                {
                    TeardownClient(keyvalue.Key, false);
                }
            }
            
            // update the bot list one time after tearing them down instead of on every one
            if (updateBotsList)
            {
                RGOverlayMenu.GetInstance()?.UpdateBots();
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
            enqueueTaskForClient(long.MaxValue, () =>
            {
                StopGameHelper();
            });
        }

        private void StartGameHelper()
        {
            // stop any old stale ones
            StopGameHelper();
            RGDebug.LogInfo($"Starting Spawnable Bots");
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            if (rgSettings.GetUseSystemSettings())
            {
                long[] botIds = rgSettings.GetBotsSelected().ToArray();
                if (botIds.Length > 0)
                {
                    // don't await here to avoid this method being defined async, which
                    // would cause big issues as the main thread Update runner wouldn't await it and
                    // gameStarted wouldn't reliably get set before they called SpawnBots
                    Task.WhenAll(botIds.Select(botId =>
                        {
                            var localBotRecord = RGBotAssetsManager.GetInstance()?.GetBotAssetRecord(botId);
                            if (localBotRecord != null)
                            {
                                //handle local bot
                                RGBotRuntimeManager.GetInstance()?.StartBot(botId);
                            }
                            else
                            {
                                // handle remote bot
                                return RGServiceManager.GetInstance()?.QueueInstantBot(
                                    (long)botId,
                                    async (botInstance) =>
                                    {
                                        AddClientConnectionForBotInstance(botInstance.id,
                                            RGClientConnectionType.REMOTE);
                                    },
                                    () =>
                                    {
                                        RGDebug.LogWarning(
                                            $"WARNING: Error starting botId: {botId}, starting without them");
                                    }

                                );
                            }
                            return null;
                        }).Where(v => v!= null));
                }
            }

            gameStarted = true;
        }

        public void StartGame()
        {
            enqueueTaskForClient(long.MaxValue, StartGameHelper);
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
            foreach (var (clientId, queue) in mainThreadTaskQueue)
            {
                if (queue.TryDequeue(out Action action))
                {
                    try
                    {
                        action.Invoke();
                    }
                    catch (Exception ex)
                    {
                        RGDebug.LogException(ex, "Exception during main thread action processing");
                    }
                }
            }
            
            // Take any requested screenshots
            _dataCollection.ProcessScreenshotRequests();
        }

        /**
         * When all game actions occur, finally decide whether or not to send the game state
         */
        private void FixedUpdate()
        {
            tick++;
            if (tick % tickRate == 0)
            {
                if (clientConnectionMap.Count > 0)
                {
                    var state = GetGameState();
                    var sceneName = SceneManager.GetActiveScene().name;
                    var tickInfoData = new RGTickInfoData(tick, sceneName, state);
                    var sentTo = new List<long>();

                    // we tried to send these out in parallel on thread pool,
                    // but scheduling the tasks on the thread took longer than
                    // doing it sequentially... by a Lot, even with 200+ bots
                    foreach (var (clientId, client) in clientConnectionMap)
                    {
                        if (client.SendTickInfo(tickInfoData))
                        {
                            sentTo.Add(clientId);
                        }
                    }

                    if (sentTo.Count > 0)
                    {
                        if (RGDebug.IsDebugEnabled)
                        {
                            RGDebug.LogDebug(
                                $"Sent RG state data from {state.Count} game objects to clients: {string.Join(",", sentTo)}");
                        }

                        if (RGDebug.IsVerboseEnabled)
                        {
                            //useful, but too spammy.. avoid building this string unless logging enabled
                            RGDebug.LogVerbose($"TickData: {tickInfoData}");
                        }
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
         * Gets the entire game state by searching for all RGEntity game objects and gathering their
         * states.
         */
        private Dictionary<string, IRGStateEntity> GetGameState()
        {
            var overlayAgent = this.gameObject.GetComponent<RGEntity>();
            
            var rgEntities = FindObjectsOfType<MonoBehaviour>(true).OfType<RGEntity>();
            var fullGameState = new Dictionary<string, IRGStateEntity>();

            // SADLY... Unity's threading model sucks and accessing the transform of an object must be done on the main thread only
            // thus, this code cannot really be run in parallel, causing a major object count scaling issue....
            foreach (var rgEntity in rgEntities)
            {
                // Give the Entity its 'core' state fields whether their are custom RGstates on the object or not
                // Custom states can then override these values
                var coreEntityState = RGState.GenerateCoreStateForRGEntity(rgEntity);
                
                if (true.Equals(rgEntity.isPlayer))
                {
                    if (!coreEntityState.ContainsKey("clientId") || coreEntityState["clientId"] == null)
                    {
                        // for things like menu bots that end up spawning a human player
                        // use the agent from the overlay
                        // Note: We have to be very careful here or we'll set this up wrong
                        // we only want to give the overlay agent to the human player.
                        // Before the clientIds are all connected, this can mess-up
                        var clientId = agentMap.FirstOrDefault(x => x.Value.Contains(overlayAgent)).Key;
                        if (clientId != null)
                        {
                            coreEntityState["clientId"] = clientId;
                            // add the agent from the player's object to the agentMap now that 
                            // we have detected that they are here 
                            // this happens for menu bots that spawn human players to control
                            // doing this allows actions from the bot code to process to the human player agent
                            // set this to avoid expensive lookups next time
                            rgEntity.ClientId = clientId;
                            agentMap[clientId].Add(rgEntity);
                        }
                    }
                }
                
                IRGStateEntity currentEntityState = null;
                
                // get the state behaviors on this game object
                var rgStates = rgEntity.gameObject.GetComponents<IRGState>();

                // build up the state for this entity from all the different state classes
                foreach (var rgState in rgStates)
                {
                    var rgStateEntity = rgState.GetGameObjectState();
                    // handle merging all the states into 1 overall state for the entity
                    // if GameObject has multiple state scripts attached to it,
                    // then we need to combine their state attributes into one IGStateEntity object.
                    // ... hopefully there is only 1, but if multiple.. the Type of the last one wins
                    // ... you still have all the data, just not easy '.' accessors for everything from the other Type(s)
                    currentEntityState?.ToList().ForEach(x =>
                    {
                        // custom states can override only some core fields
                        if (rgStateEntity.ContainsKey(x.Key))
                        {
                            RGDebug.LogWarning(
                                $"RGEntity with ObjectType {coreEntityState["type"]} has duplicate state attribute {x.Key} on {rgStateEntity.GetType().Name} and {currentEntityState.GetType().Name}.");
                        }
                        else
                        {
                            rgStateEntity[x.Key] = x.Value;
                        }
                    });
                    // update the current to the new class wrapper type
                    currentEntityState = rgStateEntity;
                }

                if (currentEntityState == null)
                {
                    // use the core state
                    currentEntityState = coreEntityState;
                }
                else
                {
                    // merge the 'core' state into this
                    foreach (var x in coreEntityState)
                    {
                        if (_overridableCoreStateFields.Contains(x.Key) && currentEntityState.ContainsKey(x.Key))
                        {
                            // keep their override
                        }
                        else
                        {
                            if (currentEntityState.ContainsKey(x.Key))
                            {
                                RGDebug.LogWarning(
                                    $"GameObject: {rgEntity.gameObject.name} with RGEntity objectType: {coreEntityState["type"]} has an RGState Behaviour that overrides core state attribute: {x.Key}; using the core state value instead.");
                            }
                            // write the value from the core state
                            currentEntityState[x.Key] = x.Value;
                        }
                    }
                }

                //update the full game state
                fullGameState[currentEntityState.id.ToString()] = currentEntityState;
            }
            return fullGameState;
        }

        public void SpawnBots(bool lateJoin = false)
        {
            enqueueTaskForClient(long.MaxValue, () =>
            {
                RGBotSpawnManager bsm = RGBotSpawnManager.GetInstance();
                if (bsm != null && gameStarted)
                {
                    bsm.SpawnBots(lateJoin);
                }
            });
        }

        private void enqueueTaskForClient(long clientId, Action task)
        {
            mainThreadTaskQueue.TryAdd(clientId, new ConcurrentQueue<Action>());
            mainThreadTaskQueue[clientId].Enqueue(task);
        }
        
        public void HandleClientTeardown(long clientId, bool doUpdateBots = true)
        {
            // Handle when the client tells us to teardown because the instant bot instance was stopped
            // This would happen when a particular bot's code determined it was finished and sent
            // us a teardown notification
            SetUnityBotState(clientId, RGUnityBotState.TEARING_DOWN);
            enqueueTaskForClient(clientId, () =>
            {
                TeardownClient(clientId, doUpdateBots);
            });
        }

        public void HandleClientHandshakeMessage(long clientId, RGClientHandshake handshakeMessage)
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
                        botName += clientIdStringSuffix;
                    }
                    Dictionary<string, object> characterConfig = handshakeMessage.characterConfig;

                    // save the token the client gave us for talking to them
                    clientConnectionMap[clientId].Token = handshakeMessage.rgToken;

                    if (!spawnable && "PERSISTENT".Equals(lifecycle))
                    {
                        // should be a menu / human simulator bot, give them the default agent... thus allowing button clicks
                        RGEntity theAgent = this.gameObject.GetComponent<RGEntity>();
                        agentMap[clientId] = new HashSet<RGEntity> { theAgent };
                    }
                    else
                    {
                        agentMap[clientId] = new HashSet<RGEntity>( );
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
                            RGDebug.LogException(e, "ERROR seating player");
                        }
                    }
                    else
                    {
                        RGDebug.LogDebug($"Sending socket handshake response to client id: {clientId}");
                        //send the client a handshake response so they can start processing
                        clientConnectionMap[clientId].SendHandshakeResponse( new RGServerHandshake(UnitySideToken, characterConfig, null));
                    }
                    
                    // we used to do this in the connection on each send, but that was too many updates.. now we do it after sending handshake response
                    SetUnityBotState(clientId, RGUnityBotState.RUNNING);
                }
                catch (Exception ex)
                {
                    RGDebug.LogWarning($"WARNING: Failed to process handshake from clientId: {clientId} - {ex}");
                }
            });

        }

        public void HandleClientValidationResult(long clientId, RGValidationResult validationResult)
        {
            enqueueTaskForClient(clientId,() =>
            {
                if (!validationResult.passed)
                {
                    RGDebug.LogDebug($"Save Validation Result for clientId: {clientId}, data: {validationResult.name}");
                    clientValidationMap[clientId]?.Enqueue(validationResult);
                }
            });
        }

        public void HandleClientActionRequest(long clientId, RGActionRequest actionRequest)
        {
            enqueueTaskForClient(clientId,() =>
            {
                RGDebug.LogDebug($"QUEUE TASK for clientId: {clientId}, data: {actionRequest}");
                HandleAction(clientId, actionRequest);
            });
        }

        public void SaveDataForTick(long clientId, RGTickInfoData tickInfoData, List<RGActionRequest> actions,
            List<RGValidationResult> validations)
        {
            _dataCollection.SaveReplayDataInfo(clientId, new RGStateActionReplayData(
                actions: actions.ToArray(),
                validationResults: validations.ToArray(),
                tickInfo: tickInfoData,
                playerId: clientId,
                
                // These three seem unused and unset in the JS version?
                sceneId: null,
                error: null,
                tickRate: null
            ));
        }

        /**
         * Maps a clientId to a specific bot for local bots. This helps with verifying that during data collection,
         * the bot and bot instance actually exists.
         */
        public void MapClientToLocalBot(long clientId, RGBot bot)
        {
            _dataCollection.RegisterBot(clientId, bot);
        }
        
        // call me on the main thread only
        private void HandleAction(long clientId, RGActionRequest actionRequest)
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
