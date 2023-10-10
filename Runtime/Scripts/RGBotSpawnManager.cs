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
         * otherwise. Equivalent to checking if <code>GetBot(clientId) != null</code>
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
         * <summary>
         * Spawns any bot that needs to be spawned from the list of connected clients.
         * </summary>
         * <param name="lateJoin">True if the bot is joining after an initial spawn of bots (i.e. during a reconnect)</param>
         */
        [MethodImpl(MethodImplOptions.Synchronized)]
        protected internal void SpawnBots(bool lateJoin = false)
        {
            // While we are using a threadsafe map, we still want to ensure that the initial spawn finishes before subsequent spawn requests
            if (lateJoin && !_initialSpawnDone)
            {
                // rg told us to spawn before the right scene.. ignore
                return;
            }
            BotInformation botInformation;
            while(_botsToSpawn.TryDequeue(out botInformation))
            {
                RGDebug.LogInfo($"Spawning bot: {botInformation.botName} for client Id: {botInformation.clientId}");
                // make sure this client is still connected
                if (true == RGBotServerListener.GetInstance()?.IsClientConnected(botInformation.clientId))
                {
                    CallSpawnBot(lateJoin, botInformation);
                }
            }
            _initialSpawnDone = true;
        }

        /**
         * <summary>
         * An internal function which calls the developer-provided spawn bot, and then holds some
         * of the information for bookkeeping. Returns null if the bot was already spawned.
         * </summary>
         * <param name="lateJoin">True if the bot is joining after an initial spawn of bots (i.e. during a reconnect)</param>
         * <param name="botInformation">The information used to spawn the bot</param>
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
                rgBotServerListener.agentMap[botInformation.clientId].Add(BotMap[botInformation.clientId].GetComponent<RGEntity>());
            }

        }

        /**
         * <summary>
         * A method that gets called when a bot's avatar should be despawned without removing its client from the game.
         * Normally used for a temporary client disconnect situation like a bot reload.
         * </summary>
         * <param name="clientId">The ID of the client that owns the bot</param>
         */
        public virtual void DeSpawnBot(uint clientId)
        {
            if (BotMap.TryRemove(clientId, out GameObject bot))
            {
                try
                {
                    Destroy(bot);
                }
                catch (Exception)
                {
                    RGDebug.LogVerbose($"Bot already de-spawned");
                }
            }
        }

        /**
         * <summary>
         * A method that gets called when a bot has received a teardown request (i.e. when the bot has signaled that
         * it is finished, or when the scene shuts down).
         * </summary>
         * <param name="clientId">The ID of the client that owns the bot</param>
         */
        public virtual void TeardownBot(uint clientId)
        {
            DeSpawnBot(clientId);
        }

        /**
         * <summary>
         * A method that gets called when the game or scene is terminated. This will remove
         * all bots from the game. In most cases, you will not need to override this method,
         * and instead should look at <see cref="TeardownBot" />.
         * </summary>
         */
        public virtual void StopGame()
        {
            RGDebug.LogInfo("Stopping the bots spawned for the current game");
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
         * <summary>
         * Returns the Instance ID of the transform for the bot in the scene. This can be used later
         * to reference the bot in the scene.
         * </summary>
         * <param name="clientId">The ID of the client that owns the bot to find</param>
         * <returns>The ID of the bot, or null if it cannot be found</returns>
         * <seealso cref="GetBot"/>
         */
        public int? GetBotId(uint clientId)
        {
            return BotMap[clientId]?.transform.GetInstanceID();
        }

        /**
         * <summary>
         * An internal function that calls the <see cref="SeatBot"/> method. Once the bot is seated, this
         * will automatically send a handshake response to the bot client to let it know that seating has
         * occurred successfully. In some cases, it may also send back modified character configurations,
         * if seatPlayer modified the BotInformation provided.
         * </summary>
         * <param name="botToSpawn">Information about the bot to spawn</param>
         * <seealso cref="BotInformation.UpdateCharacterConfig"/>
         */
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
                    RGDebug.LogDebug($"[SeatBot] Sending socket handshake response characterConfig: {botToSpawn.characterConfig} - to client id: {botToSpawn.clientId}");
                    //send the client a handshake response so they can start processing
                    rgBotServerListener.SendHandshakeResponseToClient(botToSpawn.clientId, botToSpawn.characterConfig);
                }
                
                
                // If the bot already exists, let the client know about the new ID. Otherwise, queue to respawn
                GameObject existingBot = GetBot(botToSpawn.clientId);
                if (existingBot != null)
                {
                    // get their agent re-mapped
                    rgBotServerListener.agentMap[botToSpawn.clientId].Add(existingBot.GetComponent<RGEntity>());
                }
                else
                {
                    RGDebug.LogInfo($"Enqueuing a bot to spawn: {botToSpawn.botName} for client id: {botToSpawn.clientId}");
                    _botsToSpawn.Enqueue(botToSpawn);
                }
            }
        }

        /**
         * <summary>
         * A method that gets called before spawning a bot, but after a client has connected. This is
         * useful for seating a bot into your game before their prefab has actually spawned - for instance,
         * when choosing a character in a character selection screen. This queues the bot to be spawned by
         * the <see cref="SpawnBots"/> function. You may also override configurations provided by the bot
         * using the <see cref="BotInformation.UpdateCharacterConfig"/> method, which sends a new config
         * back to your bot.
         * By default, this method does nothing when not implemented.
         * </summary>
         * <param name="botToSpawn">Information about the bot to spawn, such as the client ID, bot name, and bot config JSON</param>
         */
        public virtual void SeatBot(BotInformation botToSpawn)
        {
            // By default, seating does nothing
        }
    }
}
