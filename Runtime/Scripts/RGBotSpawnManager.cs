using System;
using System.Collections.Concurrent;
using RegressionGames.RGBotConfigs;
using RegressionGames.Types;
using UnityEngine;

namespace RegressionGames
{
    
    /**
     * The RGBotSpawnManager is the central configuration point for how bots spawn into your Unity Scene.
     * The default implementation provides the basic use case of spawning a prefab into some point in the
     * scene as a bot. Developers must implement at least `GetBotPrefab()` and `GetBotSpawn()`.
     */
    public abstract class RGBotSpawnManager : MonoBehaviour
    {

        protected static RGBotSpawnManager _this = null;
        
        private readonly ConcurrentDictionary<uint, GameObject> botMap = new ConcurrentDictionary<uint, GameObject>();
        private readonly ConcurrentQueue<BotInformation> playersToSpawn = new ConcurrentQueue<BotInformation>();

        /**
         * Returns the GameObject that Regression Games will spawn into the scene as a bot.
         * This GameObject will often have an RGAction and RGState component attached to
         * it.
         */
        public abstract GameObject GetBotPrefab();

        /**
         * Returns a Transform at which to spawn a bot into the scene.
         */
        public abstract Transform GetBotSpawn();

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

        public static RGBotSpawnManager GetInstance()
        {
            return _this;
        }

        public GameObject GetBot(uint clientId)
        {
            return botMap[clientId];
        }

        /**
         * Spawns any bot that needs to be spawned from our list of connected clients.
         * It is not common that you will need to modify this function - the `SpawnBot`
         * function is more likely to be the place where you'd like to modify your bots.
         */
        public virtual void SpawnBots(bool lateJoin = false)
        {
            BotInformation clientIdBotNamePlayerClass;
            while(playersToSpawn.TryDequeue(out clientIdBotNamePlayerClass))
            {
                // make sure this client is still connected
                if (RGBotServerListener.GetInstance().IsClientConnected(clientIdBotNamePlayerClass.clientId))
                {
                    SpawnBot(lateJoin, clientIdBotNamePlayerClass.clientId, clientIdBotNamePlayerClass.botName,
                        clientIdBotNamePlayerClass.botClass);
                }
            }
        }

        /**
         * Spawns a bot into the scene.
         */
        public virtual GameObject SpawnBot(bool lateJoin, uint clientId, string botName, string characterConfig)
        {
            // TODO: Make a warning if the spawned bot does not have any RGAction or RGState components
            var newPlayer = Instantiate(GetBotPrefab(), Vector3.zero, Quaternion.identity);
            newPlayer.transform.position = GetBotSpawn().position;
            botMap[clientId] = newPlayer;
        
            RGBotServerListener rgBotServerListener = RGBotServerListener.GetInstance();
            if (rgBotServerListener != null)
            {
                // Add the agent
                rgBotServerListener.agentMap[clientId] = botMap[clientId].GetComponent<RGAgent>();

                Debug.Log($"Sending playerId to client: {clientId}");
                // Send the client their player Id
                rgBotServerListener.SendToClient(clientId, "playerId",
                    JsonUtility.ToJson(
                        new RGServerPlayerId(botMap[clientId].transform.GetInstanceID())));
            }

            return newPlayer;
        }

        /**
         * A method that gets called when a bot has received a teardown request (i.e. when the bot has signaled that
         * it is finished, or when the scene shuts down).
         */
        public virtual void TeardownBot(uint clientId)
        {
            if (botMap.TryRemove(clientId, out GameObject bot))
            {
                try
                {
                    Destroy(bot);
                }
                catch (Exception)
                {
                    Debug.Log($"Player already de-spawned");
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
            // if there is somehow still player objects left, kill them
            foreach (uint key in botMap.Keys)
            {
                RGBotServerListener.GetInstance().EndClientConnection(key);
                TeardownBot(key);
            }
            botMap.Clear();
            playersToSpawn.Clear();
        }

        /**
         * Returns the Instance ID of the transform for the bot in the scene. This can be used later
         * to reference the bot in the scene.
         */
        public int? GetPlayerId(uint clientId)
        {
            return botMap[clientId]?.transform.GetInstanceID();
        }

        /**
         * A method that gets called before spawning a player, but after a client has connected. This is
         * useful for seating a bot into your game before their prefab has actually spawned - for instance,
         * when choosing a character in a character selection screen. This queues the bot to be spawned by
         * the `SpawnBots()` function.
         */
        public virtual BotInformation SeatPlayer(uint clientId, string characterConfig, string botName)
        {
            RGBotServerListener rgBotServerListener = RGBotServerListener.GetInstance();
            if (rgBotServerListener != null)
            {
                Debug.Log($"Sending socket handshake response to client id: {clientId}");
                //send the client a handshake response so they can start processing
                rgBotServerListener.SendHandshakeResponseToClient(clientId, characterConfig);
            }

            BotInformation botToSpawn = new BotInformation(clientId, botName, characterConfig);
            playersToSpawn.Enqueue(botToSpawn);
            return botToSpawn;
        }
    }
}
