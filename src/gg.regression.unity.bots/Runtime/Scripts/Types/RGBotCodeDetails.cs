using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGBotCodeDetails
    {
        // ReSharper disable InconsistentNaming
        public long botId;
        public long fileSize;
        public string md5;
        public DateTimeOffset? modifiedDate;
        // ReSharper enable InconsistentNaming
    }
}
