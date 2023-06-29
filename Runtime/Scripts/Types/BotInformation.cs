using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class BotInformation
    {
        public uint clientId;
        public string botName;
        public string botClass;

        public BotInformation(uint clientId, string botName, string botClass)
        {
            this.clientId = clientId;
            this.botName = botName;
            this.botClass = botClass;
        }
    }
}