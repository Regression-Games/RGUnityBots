using System;

namespace RegressionGames.Types
{

    [Serializable]
    public class RGCreateBotInstanceRequest
    {
        public long botId;
        public DateTime startDate;

        public RGCreateBotInstanceRequest(long botId, DateTime startDate)
        {
            this.botId = botId;
            this.startDate = startDate;
        }
    }
}