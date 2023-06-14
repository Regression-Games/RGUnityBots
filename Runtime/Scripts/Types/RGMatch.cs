using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGMatch
    {
        public long matchId;
        public string host;
        public int port;
        public long machineId;
    }
}

