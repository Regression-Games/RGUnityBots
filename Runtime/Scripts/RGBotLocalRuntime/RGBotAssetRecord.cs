using RegressionGames.Types;

namespace RegressionGames.RGBotLocalRuntime
{
    public class RGBotAssetRecord
    {
        public string Path;
        public RGBot BotRecord;

        public RGBotAssetRecord(string path, RGBot botRecord = null)
        {
            this.Path = path;
            if (botRecord != null)
            {
                this.BotRecord = botRecord;
            }
        }
    }
}