using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGGameplaySessionCreateRequest
    {
        public DateTimeOffset startTime;
        public DateTimeOffset endTime;
        public long numTicks;
        public long loggedWarnings;
        public long loggedErrors;

        public RGGameplaySessionCreateRequest(DateTimeOffset startTime, DateTimeOffset endTime, long numTicks, long loggedWarnings, long loggedErrors)
        {
            this.startTime = startTime;
            this.endTime = endTime;
            this.numTicks = numTicks;
            this.loggedWarnings = loggedWarnings;
            this.loggedErrors = loggedErrors;
        }
    }
}