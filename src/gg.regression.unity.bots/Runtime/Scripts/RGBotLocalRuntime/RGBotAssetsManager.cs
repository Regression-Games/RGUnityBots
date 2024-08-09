using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.Types;
using UnityEditor;

namespace RegressionGames.RGBotLocalRuntime
{
    public class RGBotAssetsManager
    {
        private static RGBotAssetsManager _this;

        private readonly Dictionary<long, RGBotAssetRecord> _botAssets = new();

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

        public List<RGBotAsset> GetAvailableBotAssets()
        {
            return _botAssets.Values.Select(v => v.BotAsset).ToList();
        }

        public List<RGBot> GetAvailableBots()
        {
            return _botAssets.Values.Select(v => v.BotAsset.Bot).ToList();
        }

        public void RefreshAvailableBots()
        {
            _botAssets.Clear();

#if UNITY_EDITOR
            // Load up the listing of available local bots
            string[] botGuids = AssetDatabase.FindAssets($"t:{typeof(RGBotAsset).FullName}", new string[] { "Assets" });
            foreach (var botGuid in botGuids)
            {
                var botAssetPath = AssetDatabase.GUIDToAssetPath(botGuid);
                // unity assets always use '/' regardless of Operating System
                var botDirectory = botAssetPath.Substring(0, botAssetPath.LastIndexOf('/'));

                try
                {
                    var botAsset = AssetDatabase.LoadAssetAtPath<RGBotAsset>(botAssetPath);

                    var botAssetRecord = new RGBotAssetRecord(botDirectory, botAsset);
                    _botAssets[botAsset.Bot.id] = botAssetRecord;
                }
                catch (Exception ex)
                {
                    RGDebug.LogWarning($"Bot at path `{botDirectory}` could not be loaded: {ex}");
                }
            }
#else
            //TODO (REG-1424): can't use asset database in real runtime
#endif
        }
    }
}
