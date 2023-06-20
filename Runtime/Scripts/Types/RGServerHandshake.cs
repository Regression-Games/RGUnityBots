using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGServerHandshake
    {
        public string token;
        // we echo this back in all cases... it is most useful to the client when randomly assigned
        // OR in bot scripts that support many types to know which type to reconnect with
        public string characterType;
        public string error;

        public RGServerHandshake( string token, string characterType, string error)
        {
            this.token = token;
            this.characterType = characterType;
            this.error = error;
        }
    }
}

