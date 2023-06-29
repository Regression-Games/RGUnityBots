using System;
using System.Collections.Concurrent;
using RegressionGames.RGBotConfigs;
using RegressionGames.Types;
using UnityEngine;

namespace RegressionGames
{
    public abstract class RGBotSpawnManager : MonoBehaviour
    {

        protected static RGBotSpawnManager _this = null;
        
        private readonly ConcurrentDictionary<uint, GameObject> botMap = new ConcurrentDictionary<uint, GameObject>();
        private readonly ConcurrentQueue<BotInformation> playersToSpawn = new ConcurrentQueue<BotInformation>();

        public abstract GameObject GetBotPrefab();

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

        public virtual GameObject SpawnBot(bool lateJoin, uint clientId, string botName, string characterConfig)
        {
            // TODO: Make a warning if the spawned bot does not have any RGAction or RGState components
            Debug.Log("Inside SpawnBot");
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

        public virtual void TeardownBot(uint clientId)
        {
            Debug.Log("Inside TeardownBot");
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

        public virtual void StopGame()
        {
            Debug.Log("Stop Game");
            // if there is somehow still player objects left, kill them
            foreach (uint key in botMap.Keys)
            {
                RGBotServerListener.GetInstance().EndClientConnection(key);
                TeardownBot(key);
            }
            botMap.Clear();
            playersToSpawn.Clear();
        }

        public int? GetPlayerId(uint clientId)
        {
            return botMap[clientId]?.transform.GetInstanceID();
        }

        public virtual BotInformation SeatPlayer(uint clientId, string characterConfig, string botName)
        {
            Debug.Log("Inside SeatPlayer");
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
