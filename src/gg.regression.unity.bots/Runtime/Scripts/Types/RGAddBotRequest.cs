using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGAddBotRequest
    {
        public long lobbyId;
        public long botId;

        public RGAddBotRequest(long lobbyId, long botId)
        {
            this.lobbyId = lobbyId;
            this.botId = botId;
        }
    }
}

