using System;

namespace RegressionGames.Types
{

    [Serializable]
    public class RGCreateBotInstanceRequest
    {
        public DateTime startDate;

        public RGCreateBotInstanceRequest(DateTime startDate)
        {
            this.startDate = startDate;
        }
    }
}