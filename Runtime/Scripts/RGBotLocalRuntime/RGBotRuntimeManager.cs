using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RegressionGames.Types;
using UnityEditor;
using UnityEngine;

namespace RegressionGames.RGBotLocalRuntime
{
    public class RGBotRuntimeManager: MonoBehaviour
    {
        private static RGBotRuntimeManager _this = null;
        
        // This must match RGBotSynchronizer.cs
        public static readonly string BOTS_PATH = "Assets/RegressionGames/Runtime/Bots";

        private readonly Dictionary<long, BotAssetRecord> _botAssets = new();
        
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
            
            // Load up the listing of available local bots
            string[] botGuids = AssetDatabase.FindAssets("BotRecord", new string[] {BOTS_PATH});
            foreach (var botGuid in botGuids)
            {
                var botAssetPath = AssetDatabase.GUIDToAssetPath(botGuid);
                var botDirectory = botAssetPath.Substring(0, botAssetPath.LastIndexOf(Path.DirectorySeparatorChar));

                try
                {
                    var botAsset = AssetDatabase.LoadAssetAtPath<RGBotAsset>(botAssetPath);

                    var botAssetRecord = new BotAssetRecord(botDirectory, botAsset.Bot);
                    _botAssets[botAsset.Bot.id] = botAssetRecord;
                }
                catch (Exception ex)
                {
                    RGDebug.LogWarning($"Bot at path `{botDirectory}` could not be loaded: {ex}");
                }
            }

        }

        public List<RGBotInstance> GetActiveBotInstances()
        {
            return _botRunners.Values.Select(v => v.BotInstance).ToList();
        }

        public List<RGBot> GetAvailableBots()
        {
            return _botAssets.Values.Select(v => v.BotRecord).ToList();
        }
        
        private long LongRandom(long min, long max, System.Random rand) {
            byte[] buf = new byte[8];
            rand.NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);

            return (System.Math.Abs(longRand % (max - min)) + min);
        }

        public long StartBot(long botId)
        {
            var botInstance = new RGBotInstance
            {
                // without a live connection to RG, we can't get a DB unique instance Id.. make this a negative random long for now
                id = LongRandom(long.MinValue, 0, new System.Random()),
                bot = null, // filled in below
                lobby = null
            };

            RGBotServerListener.GetInstance().SetUnityBotState(botInstance.id, RGUnityBotState.STARTING);

            if (_botAssets.TryGetValue(botId, out var botAssetRecord))
            {
                var botFolderNamespace =
                    botAssetRecord.Path.Substring(botAssetRecord.Path.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                
                RGUserBot userBotCode = (RGUserBot) ScriptableObject.CreateInstance($"{botFolderNamespace}.BotEntryPoint");
                userBotCode.Init(botId, botAssetRecord.BotRecord.name);
                
                botInstance.bot = botAssetRecord.BotRecord;

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

        private class BotAssetRecord
        {
            public string Path;
            public RGBot BotRecord;

            public BotAssetRecord(string path, RGBot botRecord = null)
            {
                this.Path = path;
                if (botRecord != null)
                {
                    this.BotRecord = botRecord;
                }
            }
        }

    }
}