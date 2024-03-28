using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGGameplaySessionCreateRequest
    {
        public DateTimeOffset startTime;
        public DateTimeOffset endTime;
        public long numTicks;

        public RGGameplaySessionCreateRequest(DateTimeOffset startTime, DateTimeOffset endTime, long numTicks)
        {
            this.startTime = startTime;
            this.endTime = endTime;
            this.numTicks = numTicks;
        }
    }
}