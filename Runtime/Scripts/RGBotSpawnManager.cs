using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using RegressionGames.RGBotConfigs;
using RegressionGames.Types;
using UnityEngine;

namespace RegressionGames
{
    
    /**
     * <summary>
     * The RGBotSpawnManager is the central configuration point for how bots spawn into your Unity Scene.
     * The class provides default functionality for seating bots as they join, tearing them down, and handling
     * reconnects. A developer must, at a minimum, must define how bots are spawned into the scene via the
     * `SpawnBot` method.
     * </summary>
     */
    public abstract class RGBotSpawnManager : MonoBehaviour
    {

        /**
         * Internal reference for this object, for use in singleton management.
         */
        private static RGBotSpawnManager _this = null;
        
        /**
         * A mapping from client IDs (i.e. the IDs used to identify bots connected from the Regression Games
         * backend) to the GameObjects in the scene for that bot.
         */
        public readonly ConcurrentDictionary<uint, GameObject> BotMap = new ConcurrentDictionary<uint, GameObject>();
        
        /**
         * A set of information about bots to spawn, which are eventually popped off the queue.
         */
        private readonly ConcurrentQueue<BotInformation> _botsToSpawn = new ConcurrentQueue<BotInformation>();
        
        /**
         * Tracks whether an initial set of bots have been spawned.
         */
        private bool _initialSpawnDone = false;
        
        /**
         * <summary>
         * Defines how a bot if spawned into a scene. More specifically, <paramref name="botInformation"></paramref>
         * holds the configuration for the bot, and you must implement the operations within your scene to instantiate
         * and spawn that bot. That may be simply instantiating a prefab, or spawning a NetworkObject into the scene.
         * </summary>
         * <example>
         * In this simple example, a bot is spawned by a prefab that has been linked to the component from the editor
         * <code>
         * [SerializeField]
         * [Tooltip("The character to spawn")]
         * private GameObject rgBotPrefab;
         * <br />
         * public override GameObject SpawnBot(bool lateJoin, BotInformation botInformation)
         * {
         *      var bot = Instantiate(rgBotPrefab, Vector3.zero, Quaternion.identity);
         *      bot.transform.position = botSpawnPoint.position;
         *
         *      RGPlayerMoveAction moveAction = bot.GetComponent&lt;RGPlayerMoveAction&gt;();
         *      BotCharacterConfig config = botInformation.ParseCharacterConfig&lt;BotCharacterConfig&gt;();
         *      if (config != null)
         *      {
         *          Debug.Log($"Changed speed to ${config.speed}");
         *          moveAction.speed = config.speed;
         *      }
         *      return bot;
         * }
         * </code>
         * </example>
         * <param name="lateJoin">True if the bot is joining after an initial spawn of bots (i.e. during a reconnect)</param>
         * <param name="botInformation">The id of the client, name of the bot, and a JSON string containing configuration
         *                              about your bot.</param>
         * <returns>The instantiated GameObject. This is later used to find and identify the object in your scene that
         *          is controlled by your bot.</returns>
         */
        [CanBeNull] public abstract GameObject SpawnBot(bool lateJoin, BotInformation botInformation);

        protected virtual void Awake()
        {
            // only allow 1 of these to be alive
            if( _this != null && this.gameObject != _this.gameObject)
            {
                Destroy(this.gameObject);
                return;
            }
            // keep this thing alive across scenes
            DontDestroyOnLoad(this.gameObject);
            _this = this;
        }

        /**
         * Returns the singleton instance of the RGBotSpawnManager.
         * <returns>The singleton instance of RGBotSpawnManager</returns>
         */
        public static RGBotSpawnManager GetInstance()
        {
            return _this;
        }

        /**
         * <summary>
         * Returns the GameObject of the bot that belongs to the given clientId. Returns null
         * if a bot is not found for the given clientId.
         * </summary>
         * <param name="clientId">The ID of the client that owns that bot</param>
         * <returns>The GameObject which encapsulates the bot, or null if the bot is not found</returns>
         */
        [CanBeNull]
        public GameObject GetBot(uint clientId)
        {
            if (!BotMap.ContainsKey(clientId)) return null;
            return BotMap[clientId];
        }

        /**
         * <summary>
         * Returns true if the bot owned by the given clientId has been spawned into the scene, and false
         * otherwise.
         * </summary>
         * <param name="clientId">The ID of the client that owns that bot</param>
         * <returns>True if the bot has been spawned into the scene</returns>
         */
        public bool IsBotSpawned(uint clientId)
        {
            return BotMap.ContainsKey(clientId);
        }

        /**
         * <summary>
         * Returns true if an initial spawn of bots has occurred.
         * </summary>
         * <returns>True if some bots have already been initially spawned</returns>
         */
        public bool BotsHaveSpawned()
        {
            return _initialSpawnDone;
        }

        /**
         * Spawns any bot that needs to be spawned from our list of connected clients.
         */
        [MethodImpl(MethodImplOptions.Synchronized)]
        protected internal void SpawnBots(bool lateJoin = false)
        {
            // While we are using a threadsafe map, we still want to ensure that the initial spawn finishes before subsequent spawn requests
            if (lateJoin && !_initialSpawnDone)
            {
                Debug.Log("These bots are late joining but the initial spawn is not done - ignore this");
                // rg told us to spawn before the right scene.. ignore
                return;
            }
            BotInformation botInformation;
            while(_botsToSpawn.TryDequeue(out botInformation))
            {
                Debug.Log("[SpawnBots] Spawning all queued bots");
                // make sure this client is still connected
                if (RGBotServerListener.GetInstance().IsClientConnected(botInformation.clientId))
                {
                    CallSpawnBot(lateJoin, botInformation);
                }
            }
            _initialSpawnDone = true;
        }

        /**
         * An internal function which calls the developer-provided spawn bot, and then holds some
         * of the information for bookkeeping. Returns null if the bot was already spawned.
         */
        private void CallSpawnBot(bool lateJoin, BotInformation botInformation)
        {
            GameObject bot = GetBot(botInformation.clientId);
            if (bot == null)
            {
                bot = SpawnBot(lateJoin, botInformation);
                if (bot == null) return;
                BotMap[botInformation.clientId] = bot;
            }
            
            RGBotServerListener rgBotServerListener = RGBotServerListener.GetInstance();
            if (rgBotServerListener != null)
            {
                // Add the agent
                rgBotServerListener.agentMap[botInformation.clientId] = BotMap[botInformation.clientId].GetComponent<RGAgent>();

                Debug.Log($"Sending playerId to client: {botInformation.clientId}");
                // Send the client their player Id
                rgBotServerListener.SendToClient(botInformation.clientId, "playerId",
                    JsonUtility.ToJson(
                        new RGServerPlayerId(bot.transform.GetInstanceID())));
            }

        }

        /**
         * A method that gets called when a bot has received a teardown request (i.e. when the bot has signaled that
         * it is finished, or when the scene shuts down).
         */
        public virtual void TeardownBot(uint clientId)
        {
            if (BotMap.TryRemove(clientId, out GameObject bot))
            {
                try
                {
                    Destroy(bot);
                }
                catch (Exception)
                {
                    Debug.Log($"Bot already de-spawned");
                }
            }
        }

        /**
         * A method that gets called with the game or scene is terminated. This will remove
         * all bots from the game. In most cases, you will not need to override this method,
         * and instead should look at TeardownBot().
         */
        public virtual void StopGame()
        {
            Debug.Log("Stopping the game");
            // if there is somehow still bot objects left, kill them
            foreach (uint key in BotMap.Keys)
            {
                RGBotServerListener.GetInstance().EndClientConnection(key);
                TeardownBot(key);
            }
            BotMap.Clear();
            _botsToSpawn.Clear();
            _initialSpawnDone = false;
        }

        /**
         * Returns the Instance ID of the transform for the bot in the scene. This can be used later
         * to reference the bot in the scene.
         */
        public int? GetBotId(uint clientId)
        {
            return BotMap[clientId]?.transform.GetInstanceID();
        }

        protected internal void CallSeatBot(BotInformation botToSpawn)
        {
            lock (string.Intern($"{botToSpawn.clientId}"))
            {
                // First, allow the developer to configure any frontend/backend for a player about to spawn,
                // such as character selection.
                SeatBot(botToSpawn);
                RGBotServerListener rgBotServerListener = RGBotServerListener.GetInstance();
                if (rgBotServerListener != null)
                {
                    Debug.Log($"[SeatBot] Sending socket handshake response to client id: {botToSpawn.clientId}");
                    //send the client a handshake response so they can start processing
                    rgBotServerListener.SendHandshakeResponseToClient(botToSpawn.clientId, botToSpawn.characterConfig);
                }
                
                
                // If the bot already exists, let the client know about the new ID. Otherwise, queue to respawn
                GameObject existingBot = GetBot(botToSpawn.clientId);
                if (existingBot != null)
                {
                    Debug.Log($"Sending playerId to client again: {botToSpawn.clientId}");
                    // Send the client their player Id
                    rgBotServerListener.SendToClient(botToSpawn.clientId, "playerId",
                        JsonUtility.ToJson(
                            new RGServerPlayerId(existingBot.transform.GetInstanceID())));
                }
                else
                {
                    Debug.Log($"Enqueuing spawning a bot: {botToSpawn.botName}");
                    _botsToSpawn.Enqueue(botToSpawn);
                }
            }
        }

        /**
         * A method that gets called before spawning a bot, but after a client has connected. This is
         * useful for seating a bot into your game before their prefab has actually spawned - for instance,
         * when choosing a character in a character selection screen. This queues the bot to be spawned by
         * the `SpawnBots()` function.
         */
        public virtual void SeatBot(BotInformation botToSpawn)
        {
            // By default, seating does nothing
        }
    }
}
