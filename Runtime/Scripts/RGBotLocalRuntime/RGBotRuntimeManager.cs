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

        public long StartBot(long botId)
        {
            var botInstance = new RGBotInstance
            {
                // without a live connection to RG, we can't get a DB unique instance Id.. make this a negative random long for now
                id = RGRuntimeProperties.GetNextBotInstanceId(),
                bot = null, // filled in below
                lobby = null,
                createdDate = DateTimeOffset.Now
            };

            RGBotServerListener.GetInstance().SetUnityBotState(botInstance.id, RGUnityBotState.STARTING);

            var botAssetRecord = RGBotAssetsManager.GetInstance().GetBotAssetRecord(botId);
            if (botAssetRecord != null)
            {
 
                // Handle bot namespace with priority being botName_botId
                // but fall back to botDirectoryName as that is the namespace before bots are synced
                // to RG as we don't know the real botId yet                
                RGUserBot userBotCode = null;
                // if negative, replace the minus sign with an 'n'
                var botIdKey = (botAssetRecord.BotAsset.Bot.id < 0)
                    ? $"_n{-1 * botAssetRecord.BotAsset.Bot.id}"
                    : $"_{botAssetRecord.BotAsset.Bot.id}";
                var botNameSpace = botAssetRecord.BotAsset.Bot.name + botIdKey;
                // unity assets always use '/' regardless of Operating System
                var botFolderNamespace =
                    botAssetRecord.Path.Substring(botAssetRecord.Path.LastIndexOf('/') + 1);
                try
                {
                    userBotCode = (RGUserBot)ScriptableObject.CreateInstance($"{botNameSpace}.BotEntryPoint");
                }
                catch (Exception e)
                {
                    // nothing to see here, unity already logs the failure above in the log despite usually not throwing an exception
                }

                if (userBotCode == null)
                {
                    RGDebug.LogWarning($"Namespace botName_botId not found for {botNameSpace}, using directory name as namespace instead {botFolderNamespace}");
                    userBotCode = (RGUserBot)ScriptableObject.CreateInstance($"{botFolderNamespace}.BotEntryPoint");
                }

                userBotCode.Init(botId, botAssetRecord.BotAsset.Bot.name);
                
                botInstance.bot = botAssetRecord.BotAsset.Bot;

                RGClientConnection connection = RGBotServerListener.GetInstance()
                    .AddClientConnectionForBotInstance(botInstance.id, RGClientConnectionType.LOCAL);
                
                // Also attach the bot so we can upload later
                RGBotServerListener.GetInstance().MapClientToLocalBot(botInstance.id, botAssetRecord.BotAsset.Bot);

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