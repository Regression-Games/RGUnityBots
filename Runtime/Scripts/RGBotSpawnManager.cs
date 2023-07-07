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
     * The RGBotSpawnManager is the central configuration point for how bots spawn into your Unity Scene.
     * The default implementation provides the basic use case of spawning a prefab into some point in the
     * scene as a bot. Developers must implement at least `GetBotPrefab()` and `GetBotSpawn()`.
     */
    public abstract class RGBotSpawnManager : MonoBehaviour
    {

        protected static RGBotSpawnManager _this = null;
        
        public readonly ConcurrentDictionary<uint, GameObject> botMap = new ConcurrentDictionary<uint, GameObject>();
        private readonly ConcurrentQueue<BotInformation> botsToSpawn = new ConcurrentQueue<BotInformation>();
        
        // Tracks whether or not any bot has been spawned yet
        private bool initialSpawnDone = false;

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

        [CanBeNull]
        public GameObject GetBot(uint clientId)
        {
            if (!botMap.ContainsKey(clientId)) return null;
            return botMap[clientId];
        }

        public bool IsBotSpawned(uint clientId)
        {
            return botMap.ContainsKey(clientId);
        }

        public bool BotsHaveSpawned()
        {
            return initialSpawnDone;
        }

        /**
         * Spawns any bot that needs to be spawned from our list of connected clients.
         */
        [MethodImpl(MethodImplOptions.Synchronized)]
        protected internal void SpawnBots(bool lateJoin = false)
        {
            // While we are using a threadsafe map, we still want to ensure that the initial spawn finishes before subsequent spawn requests
            if (lateJoin && !initialSpawnDone)
            {
                // rg told us to spawn before the right scene.. ignore
                return;
            }
            BotInformation botInformation;
            while(botsToSpawn.TryDequeue(out botInformation))
            {
                // make sure this client is still connected
                if (RGBotServerListener.GetInstance().IsClientConnected(botInformation.clientId))
                {
                    CallSpawnBot(lateJoin, botInformation);
                }
            }
            initialSpawnDone = true;
        }

        /**
         * An internal function which calls the developer-provided spawn bot, and then holds some
         * of the information for bookkeeping. Returns null if the bot was already spawned.
         */
        private void CallSpawnBot(bool lateJoin, BotInformation botInformation)
        {
            // if (botMap.ContainsKey(botInformation.clientId))
            // {
            //     return null;
            // }
            GameObject spawnedBot = SpawnBot(lateJoin, botInformation);
            if (spawnedBot == null) return;
            botMap[botInformation.clientId] = spawnedBot;
            RGBotServerListener rgBotServerListener = RGBotServerListener.GetInstance();
            if (rgBotServerListener != null)
            {
                // Add the agent
                rgBotServerListener.agentMap[botInformation.clientId] = botMap[botInformation.clientId].GetComponent<RGAgent>();

                Debug.Log($"Sending playerId to client: {botInformation.clientId}");
                // Send the client their player Id
                rgBotServerListener.SendToClient(botInformation.clientId, "playerId",
                    JsonUtility.ToJson(
                        new RGServerPlayerId(spawnedBot.transform.GetInstanceID())));
            }

        }

        /**
         * Spawns a bot into the scene.
         */
        [CanBeNull] public abstract GameObject SpawnBot(bool lateJoin, BotInformation botInformation);

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
            // if there is somehow still bot objects left, kill them
            foreach (uint key in botMap.Keys)
            {
                RGBotServerListener.GetInstance().EndClientConnection(key);
                TeardownBot(key);
            }
            botMap.Clear();
            botsToSpawn.Clear();
            initialSpawnDone = false;
        }

        /**
         * Returns the Instance ID of the transform for the bot in the scene. This can be used later
         * to reference the bot in the scene.
         */
        public int? GetBotId(uint clientId)
        {
            return botMap[clientId]?.transform.GetInstanceID();
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
                    Debug.Log($"Sending socket handshake response to client id: {botToSpawn.clientId}");
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
                    botsToSpawn.Enqueue(botToSpawn);
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
