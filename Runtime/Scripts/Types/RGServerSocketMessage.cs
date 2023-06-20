using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGServerSocketMessage
    {
        public string token;
        public string type;
        public string data;

        public RGServerSocketMessage(string token, string type, string data)
        {
            this.token = token;
            this.type = type;
            this.data = data;
        }
    }
}

