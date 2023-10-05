using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGClientSocketMessage
    {
        public string token;
        public string type;
        public long clientId;
        public string data;
    }
}

