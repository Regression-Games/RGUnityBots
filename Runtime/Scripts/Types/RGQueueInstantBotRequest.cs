using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGQueueInstantBotRequest
    {
        public string gameEngineHost;
        public int gameEnginePort;
        public long botId;
        public string externalAuthToken;

        public RGQueueInstantBotRequest(string gameEngineHost, int gameEnginePort, long botId, string externalAuthToken)
        {
            this.gameEngineHost = gameEngineHost;
            this.gameEnginePort = gameEnginePort;
            this.botId = botId;
            this.externalAuthToken = externalAuthToken;
        }
    }
}
