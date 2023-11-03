using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RegressionGames.Types;
using UnityEngine;
using Random = System.Random;

namespace RegressionGames.RGBotLocalRuntime
{
    [HelpURL("https://docs.regression.gg/studios/unity/unity-sdk/overview")]
    public class RGBotRuntimeManager: MonoBehaviour
    {
        private static RGBotRuntimeManager _this = null;
        
        private readonly ConcurrentDictionary<long, RGBotRunner> _botRunners = new();

        public static RGBotRuntimeManager GetInstance()
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

            RGBotAssetsManager.GetInstance().RefreshAvailableBots();

        }

        public List<RGBotInstance> GetActiveBotInstances()
        {
            return _botRunners.Values.Select(v => v.BotInstance).ToList();
        }

        private long LongRandom(long min, long max, Random rand) {
            byte[] buf = new byte[8];
            rand.NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);

            return (Math.Abs(longRand % (max - min)) + min);
        }

        public long StartBot(long botId)
        {
            var botInstance = new RGBotInstance
            {
                // without a live connection to RG, we can't get a DB unique instance Id.. make this a negative random long for now
                id = RGSettings.GetOrCreateSettings().GetNextBotInstanceId(),
                bot = null, // filled in below
                lobby = null,
                createdDate = DateTimeOffset.Now
            };

            RGBotServerListener.GetInstance().SetUnityBotState(botInstance.id, RGUnityBotState.STARTING);

            var botAssetRecord = RGBotAssetsManager.GetInstance().GetBotAssetRecord(botId);
            if (botAssetRecord != null)
            {
                var botFolderNamespace =
                    botAssetRecord.Path.Substring(botAssetRecord.Path.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                
                RGUserBot userBotCode = (RGUserBot) ScriptableObject.CreateInstance($"{botFolderNamespace}.BotEntryPoint");
                userBotCode.Init(botId, botAssetRecord.BotAsset.Bot.name);
                
                botInstance.bot = botAssetRecord.BotAsset.Bot;

                RGClientConnection connection = RGBotServerListener.GetInstance()
                    .AddClientConnectionForBotInstance(botInstance.id, RGClientConnectionType.LOCAL);

                var botRunner = new RGBotRunner(botInstance, userBotCode,
                    () => { _botRunners.TryRemove(botInstance.id, out _); });
                (connection as RGClientConnection_Local)?.SetBotRunner(botRunner);

                _botRunners.TryAdd(botInstance.id, botRunner);
                botRunner.StartBot();

                return botInstance.id;
            }
            else
            {
                RGDebug.LogWarning($"Unable to start Bot Id: {botId}.  Local definition for bot not found");
            }

            return 0;
        }
        
        private void OnDrawGizmos()
        {
            foreach (var botRunner in _botRunners.Values)
            {
                botRunner.OnDrawGizmos();
            }
        }
        
    }
}