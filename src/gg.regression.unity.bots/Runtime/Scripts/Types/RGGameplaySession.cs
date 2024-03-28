using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGGameplaySession
    {
        public long id;
        public DateTime startTime;
        public DateTime endTime;
        public long numTicks;
    }
}