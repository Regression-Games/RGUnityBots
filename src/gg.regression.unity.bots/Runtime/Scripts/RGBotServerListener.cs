using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using RegressionGames.DataCollection;
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.StateActionTypes;
using RegressionGames.Types;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RegressionGames
{
    [HelpURL("https://docs.regression.gg/studios/unity/unity-sdk/overview")]
    public class RGBotServerListener : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Send a state update every X ticks")]
        public int tickRate = 50;

        public readonly ConcurrentDictionary<long?, HashSet<GameObject>> AgentMap = new ();

        private long _tick;

        private static RGBotServerListener _this;

        private RGDataCollection _dataCollection;

        public static RGBotServerListener GetInstance()
        {
            return _this;
        }

        public void Start()
        {
            _dataCollection = new RGDataCollection();
            // initialize/load all the states/actions in this project runtime
            BehavioursWithStateOrActions.Initialize();
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

        private bool _gameStarted;

        public string UnitySideToken { get; private set; }= Guid.NewGuid().ToString();

        private readonly ConcurrentDictionary<long, RGUnityBotState> _botStates = new();

        private readonly ConcurrentDictionary<long, List<Action<RGUnityBotState>>> _botStateListeners = new();

        public void AddUnityBotStateListener(long id, Action<RGUnityBotState> func)
        {
            _botStateListeners.AddOrUpdate(id, new List<Action<RGUnityBotState>> {func}, (key, oldValue) =>
            {
                oldValue.Add(func);
                return oldValue;
            });
        }

        public RGUnityBotState GetUnityBotState(long id)
        {
            if (_botStates.TryGetValue(id, out RGUnityBotState state))
            {
                return state;
            }
            return RGUnityBotState.UNKNOWN;
        }
        
        public void SetUnityBotState(long id, RGUnityBotState state)
        {
            if (_botStates.TryGetValue(id, out var oldValue))
            {
                if (!oldValue.Equals(state))
                {
                    _botStates[id] = state;
                    SendStateUpdatesToListeners(id, state);
                }
            }
            else
            {
                _botStates[id] = state;
                SendStateUpdatesToListeners(id, state);
            }
        }

        private void SendStateUpdatesToListeners(long id, RGUnityBotState newState)
        {
            if (_botStateListeners.TryGetValue(id, out List<Action<RGUnityBotState>> funcList))
            {
                foreach (var action in funcList)
                {
                    try
                    {
                        EnqueueTaskForClient(id, () => action.Invoke(newState));
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
        [ItemCanBeNull] private readonly ConcurrentDictionary<long, RGClientConnection> _clientConnectionMap = new ();
        
        [ItemCanBeNull] private readonly ConcurrentDictionary<long?, ConcurrentQueue<RGValidationResult>> _clientValidationMap = new ();
 
        // keep these in a map by clientId so that we can do 1 action per client per update call
        private readonly ConcurrentDictionary<long, ConcurrentQueue<Action>> _mainThreadTaskQueue = new ();

        public RGClientConnection GetClientConnection(long clientId)
        {
            if (_clientConnectionMap.TryGetValue(clientId, out RGClientConnection result))
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
            
            _clientConnectionMap.AddOrUpdate(botInstanceId, connection, (k,v) =>
            {
                return v;
            });
            _clientValidationMap.AddOrUpdate(botInstanceId, new ConcurrentQueue<RGValidationResult>(), (k, v) => 
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
            return !_clientConnectionMap.IsEmpty;
        }

        public ConcurrentQueue<RGValidationResult> GetFailedValidationsForClient(long clientId)
        {
            if (_clientValidationMap.TryGetValue(clientId, out var validations))
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
            _clientConnectionMap.Clear();
            _clientValidationMap.Clear();
            _botStateListeners.Clear();
            _botStates.Clear();
            _mainThreadTaskQueue.Clear();
            
            UnitySideToken = Guid.NewGuid().ToString();
        }

        public void EndAllClientConnections()
        {
            List<long> clientIds = _clientConnectionMap.Keys.ToList();
            foreach (var clientId in clientIds)
            {
                EndClientConnection(clientId);
            }
        }

        public void EndClientConnection(long clientId)
        {
            if (_clientConnectionMap.TryRemove(clientId, out var clientConnection))
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
            
            AgentMap.TryRemove(clientId, out _);
            _botStateListeners.TryRemove(clientId, out _);
            _botStates.TryRemove(clientId, out _);
            _mainThreadTaskQueue.TryRemove(clientId, out _);

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
            foreach (var (clientId, rgClientConnection) in _clientConnectionMap)
            {
                SetUnityBotState(clientId, RGUnityBotState.TEARING_DOWN);
            }

            EnqueueTaskForClient(0, () =>
            {
                // do these all together on a single main thread update
                foreach (var (clientId, rgClientConnection) in _clientConnectionMap)
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
            _gameStarted = false;
            
            foreach (var keyvalue in _clientConnectionMap)
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
            EnqueueTaskForClient(long.MaxValue, () =>
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

            _gameStarted = true;
        }

        public void StartGame()
        {
            EnqueueTaskForClient(long.MaxValue, StartGameHelper);
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
                foreach (var keyvalue in _clientConnectionMap)
                {
                    if (!keyvalue.Value.Connected())
                    {
                        RGDebug.LogVerbose($"Update needs to connect client for botInstanceId: {keyvalue.Key}");
                        _ = SetupClientConnection(keyvalue.Value);
                    }
                }

                lastClientConnectTime = time;
            }

            if (_gameStarted)
            {
                SpawnBots(true);
            }

            // for each client, run up to 1 action per update
            foreach (var (clientId, queue) in _mainThreadTaskQueue)
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
            _tick++;
            if (_tick % tickRate == 0)
            {
                if (_clientConnectionMap.Count > 0)
                {
                    var state = GetGameState();
                    var sceneName = SceneManager.GetActiveScene().name;
                    var tickInfoData = new RGTickInfoData(_tick, sceneName, state);
                    var sentTo = new List<long>();

                    // we tried to send these out in parallel on thread pool,
                    // but scheduling the tasks on the thread took longer than
                    // doing it sequentially... by a Lot, even with 200+ bots
                    foreach (var (clientId, client) in _clientConnectionMap)
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
         * Gets the entire game state for each GameObject with RG components on it or in its behaviours.
         */
        private Dictionary<string, RGStateEntity_Core> GetGameState()
        {
            var overlayAgent = this.gameObject;
            var fullGameState = new Dictionary<string, RGStateEntity_Core>();
            
            // find all Buttons
            var allButtons = RGFindUtils.Instance.FindAllButtons();
            
            foreach (var button in allButtons)
            {
                var statefulGameObject = button.gameObject;
                var gameObjectId = statefulGameObject.transform.GetInstanceID();
                var gameObjectIdString = gameObjectId.ToString();
                
                // Give the GameObject its 'core' state fields
                if (!fullGameState.TryGetValue(gameObjectIdString, out var coreEntityState))
                {
                    coreEntityState = RGStateHandler.GetCoreStateForGameObject(statefulGameObject, button);
                    fullGameState[gameObjectIdString] = coreEntityState;
                }
            }

            // get behaviours with attributes or custom RGState classes
            var statefulBehaviours = RGFindUtils.Instance.FindStatefulAndActionableBehaviours();

            // SADLY... Unity's threading model stinks and accessing the transform of an object must be done on the main thread only
            // thus, this parts of this code cannot really be run in parallel, causing a potential state object count scaling performance issue....
            foreach (var statefulBehaviour in statefulBehaviours)
            {
                var statefulGameObject = statefulBehaviour.gameObject;
                var gameObjectId = statefulGameObject.transform.GetInstanceID();
                var gameObjectIdString = gameObjectId.ToString();
                // if the full game state doesn't already have this entry
                
                // Give the GameObject its 'core' state fields
                if (!fullGameState.TryGetValue(gameObjectIdString, out var coreEntityState))
                {
                    coreEntityState = RGStateHandler.GetCoreStateForGameObject(statefulGameObject);
                    fullGameState[gameObjectIdString] = coreEntityState;
                }

                var isPlayerBehaviour = false;

                if (statefulBehaviour is IRGStateBehaviour stateBehaviour)
                {
                    // custom state class
                    stateBehaviour.PopulateStateEntity(coreEntityState, out isPlayerBehaviour);
                }
                else
                {
                    // some other MonoBehaviour with [RGState] or [RGAction] attributes in it
                    RGStateHandler.PopulateStateEntityForStatefulObject(coreEntityState, statefulBehaviour,
                        out isPlayerBehaviour);
                }

                // set the core state value for is player if any behaviour on this gameObject says it is
                if (isPlayerBehaviour)
                {
                    coreEntityState["isPlayer"] = true;
                }

                // any of the stateful objects on a game object could set this
                var isPlayer = (bool)coreEntityState["isPlayer"];

                if (isPlayer)
                {
                    if (!coreEntityState.ContainsKey("clientId") || coreEntityState["clientId"] == null)
                    {
                        // for things like menu bots that end up spawning a human player
                        // use the agent from the overlay
                        // Note: We have to be very careful here or we'll set this up wrong
                        // we only want to give the overlay agent to the human player.
                        // Before the clientIds are all connected, this can mess-up
                        var clientId = AgentMap.FirstOrDefault(x => x.Value.Contains(overlayAgent)).Key;
                        if (clientId != null)
                        {
                            coreEntityState["clientId"] = clientId;
                            // add the agent from the player's object to the agentMap now that 
                            // we have detected that they are here 
                            // this happens for menu bots that spawn human players to control
                            // doing this allows actions from the bot code to process to the human player agent
                            // set this to avoid expensive lookups next time
                            RGStateHandler.EnsureRGStateHandlerOnGameObject(statefulGameObject).ClientId = clientId;
                            AgentMap[clientId].Add(statefulGameObject);
                        }
                    }
                }
            }
            return fullGameState;
        }

        public void SpawnBots(bool lateJoin = false)
        {
            EnqueueTaskForClient(long.MaxValue, () =>
            {
                RGBotSpawnManager bsm = RGBotSpawnManager.GetInstance();
                if (bsm != null && _gameStarted)
                {
                    bsm.SpawnBots(lateJoin);
                }
            });
        }

        private void EnqueueTaskForClient(long clientId, Action task)
        {
            _mainThreadTaskQueue.TryAdd(clientId, new ConcurrentQueue<Action>());
            _mainThreadTaskQueue[clientId].Enqueue(task);
        }
        
        public void HandleClientTeardown(long clientId, bool doUpdateBots = true)
        {
            // Handle when the client tells us to teardown because the instant bot instance was stopped
            // This would happen when a particular bot's code determined it was finished and sent
            // us a teardown notification
            SetUnityBotState(clientId, RGUnityBotState.TEARING_DOWN);
            EnqueueTaskForClient(clientId, () =>
            {
                TeardownClient(clientId, doUpdateBots);
            });
        }

        public void HandleClientHandshakeMessage(long clientId, RGClientHandshake handshakeMessage)
        {
            // can only call Unity APIs on main thread, so queue this up
            EnqueueTaskForClient(clientId,() =>
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
                    
                    _clientConnectionMap[clientId].Lifecycle = lifecycle;

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
                    _clientConnectionMap[clientId].Token = handshakeMessage.rgToken;

                    if (!spawnable && "PERSISTENT".Equals(lifecycle))
                    {
                        // should be a menu / human simulator bot, give them the default agent... thus allowing button clicks
                        AgentMap[clientId] = new HashSet<GameObject> { this.gameObject };
                    }
                    else
                    {
                        AgentMap[clientId] = new HashSet<GameObject>( );
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
                        _clientConnectionMap[clientId].SendHandshakeResponse( new RGServerHandshake(UnitySideToken, characterConfig, null));
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
            EnqueueTaskForClient(clientId,() =>
            {
                if (!validationResult.passed)
                {
                    RGDebug.LogDebug($"Save Validation Result for clientId: {clientId}, data: {validationResult.name}");
                    _clientValidationMap[clientId]?.Enqueue(validationResult);
                }
            });
        }

        public void HandleClientActionRequest(long clientId, RGActionRequest actionRequest)
        {
            EnqueueTaskForClient(clientId,() =>
            {
                RGDebug.LogDebug($"QUEUE TASK for clientId: {clientId}, data: {actionRequest}");
                HandleActionForClient(clientId, actionRequest);
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
        private void HandleActionForClient(long clientId, RGActionRequest actionRequest)
        {
            var agents = AgentMap[clientId];
            /* TODO: Right now this broadcasts the action to all agents for a clientId.  We may
             * want to assign actions to specific agents based on their entity Id
             * if we ever support more than 1 entity + button clicks
             */
            foreach (var agent in agents)
            {
                HandleActionOnGameObject(agent, actionRequest);
            }
        }

        public static void HandleActionOnGameObject(GameObject gameObject, RGActionRequest actionRequest)
        {
            //make sure this agent isn't being destroyed already
            if (gameObject != null)
            {
                var actionHandler = gameObject.GetComponent<RGActionHandler>();
                // make sure the gameObject has an ActionHandler ready to go
                if (actionHandler == null)
                {
                    actionHandler = gameObject.AddComponent<RGActionHandler>();
                    // call start right away so we don't have a race condition of the actions not loading in time
                    actionHandler.Start();
                }

                actionHandler.Invoke(actionRequest);
            }
        }
        
    }

}
