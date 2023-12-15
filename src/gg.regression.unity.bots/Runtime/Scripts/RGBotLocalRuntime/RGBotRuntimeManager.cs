using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.Types;
using UnityEditor;
using UnityEngine;

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
                // Check if the bot has a controller. If it does, it will be handling all this logic
                if (botAssetRecord.BotAsset.botController != null)
                {
                    StartControlledBot(botId, botInstance, botAssetRecord);
                }
                else {
                    // TODO: The rest of this code could be bundled up into a "default" Bot Controller we use if the user doesn't specify one

                    // Find the bot entry point script
                    var entryPointPath = $"{botAssetRecord.Path}/BotEntryPoint.cs";
                    var entryPointScript = AssetDatabase.LoadAssetAtPath<MonoScript>(entryPointPath);
                    if (entryPointScript == null)
                    {
                        RGDebug.LogError($"Could not find bot entry point script at {entryPointPath}.");
                        return 0;
                    }

                    var entryPointType = entryPointScript.GetClass();
                    RGDebug.LogVerbose($"Bot Entry Point {entryPointType.FullName} located at {entryPointPath}");

                    RGUserBot userBotCode = null;
                    try
                    {
                        userBotCode = (RGUserBot)ScriptableObject.CreateInstance(entryPointType);
                    }
                    catch (Exception e)
                    {
                        // nothing to see here, unity already logs the failure above in the log despite usually not throwing an exception
                    }

                    if (userBotCode == null)
                    {
                        RGDebug.LogError($"Failed to create instance of bot entry point '{entryPointType.FullName}'.");
                        return 0;
                    }

                    userBotCode.Init(botId, botAssetRecord.BotAsset.Bot.name);

                    botInstance.bot = botAssetRecord.BotAsset.Bot;


                    RGClientConnection connection = RGBotServerListener.GetInstance()
                        .AddClientConnectionForBotInstance(botInstance.id, null, RGClientConnectionType.LOCAL);

                    // Also attach the bot so we can upload later
                    RGBotServerListener.GetInstance().MapClientToLocalBot(botInstance.id, botAssetRecord.BotAsset.Bot);

                    var botRunner = new RGBotRunner(botInstance, userBotCode,
                        () => { _botRunners.TryRemove(botInstance.id, out _); });
                    (connection as RGClientConnection_Local)?.SetBotRunner(botRunner);

                    _botRunners.TryAdd(botInstance.id, botRunner);
                    botRunner.StartBot();
                }

                return botInstance.id;
            }

            RGDebug.LogWarning($"Unable to start Bot Id: {botId}.  Local definition for bot not found");

            return 0;
        }

        private void StartControlledBot(long botId, RGBotInstance botInstance, RGBotAssetRecord botAssetRecord)
        {
            var botController = Instantiate(botAssetRecord.BotAsset.botController);
            botController.SetBotInstance(botInstance);

            RGBotServerListener.GetInstance()
                .AddClientConnectionForBotInstance(botInstance.id, botController, RGClientConnectionType.LOCAL);

            // Also attach the bot so we can upload later
            RGBotServerListener.GetInstance().MapClientToLocalBot(botInstance.id, botAssetRecord.BotAsset.Bot);
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
