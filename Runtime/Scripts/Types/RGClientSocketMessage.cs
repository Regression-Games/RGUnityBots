using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGClientSocketMessage
    {
        public string token;
        public string type;
        public uint clientId;
        public string data;
    }
}

