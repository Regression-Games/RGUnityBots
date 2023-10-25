using RegressionGames.Types;

namespace RegressionGames.RGBotLocalRuntime
{
    public class RGBotAssetRecord
    {
        public string Path;
        public RGBotAsset BotAsset;

        public RGBotAssetRecord(string path, RGBotAsset botAsset = null)
        {
            this.Path = path;
            if (botAsset != null)
            {
                this.BotAsset = botAsset;
            }
        }
    }
}