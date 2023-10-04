using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RegressionGames.Types;
using UnityEditor;

namespace RegressionGames.RGBotLocalRuntime
{
    public class RGBotAssetsManager
    {
        private static RGBotAssetsManager _this;
        
        private readonly Dictionary<long, RGBotAssetRecord> _botAssets = new();
        
        // This must match RGBotSynchronizer.cs
        public static readonly string BOTS_PATH = "Assets/RegressionGames/Runtime/Bots";

        public static RGBotAssetsManager GetInstance()
        {
            if (_this == null)
            {
                _this = new RGBotAssetsManager();
            }

            return _this;
        }

        private RGBotAssetsManager()
        {
            RefreshAvailableBots();
        }

        public RGBotAssetRecord GetBotAssetRecord(long botId)
        {
            if (_botAssets.TryGetValue(botId, out var value))
            {
                return value;
            }

            return null;
        }

        public List<RGBot> GetAvailableBots()
        {
            return _botAssets.Values.Select(v => v.BotRecord).ToList();
        }
        
        public void RefreshAvailableBots()
        {
            // Load up the listing of available local bots
            string[] botGuids = AssetDatabase.FindAssets("BotRecord", new string[] {BOTS_PATH});
            foreach (var botGuid in botGuids)
            {
                var botAssetPath = AssetDatabase.GUIDToAssetPath(botGuid);
                var botDirectory = botAssetPath.Substring(0, botAssetPath.LastIndexOf(Path.DirectorySeparatorChar));

                try
                {
                    var botAsset = AssetDatabase.LoadAssetAtPath<RGBotAsset>(botAssetPath);

                    var botAssetRecord = new RGBotAssetRecord(botDirectory, botAsset.Bot);
                    _botAssets[botAsset.Bot.id] = botAssetRecord;
                }
                catch (Exception ex)
                {
                    RGDebug.LogWarning($"Bot at path `{botDirectory}` could not be loaded: {ex}");
                }
            }
        }
    }
}