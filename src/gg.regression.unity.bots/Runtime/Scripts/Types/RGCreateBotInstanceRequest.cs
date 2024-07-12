using System;

namespace RegressionGames.Types
{

    [Serializable]
    public class RGCreateBotInstanceRequest
    {
        public Long botId;
        public DateTime startDate;

        public RGCreateBotInstanceRequest(Long botId, DateTime startDate)
        {
            this.botId = botId;
            this.startDate = startDate;
        }
    }
}