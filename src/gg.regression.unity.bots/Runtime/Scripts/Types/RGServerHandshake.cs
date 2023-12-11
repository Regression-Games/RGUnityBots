using System;
using System.Collections.Generic;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGServerHandshake
    {
        // ReSharper disable InconsistentNaming
        public string token;

        // we echo this back in all cases... it is most useful to the client when randomly assigned
        // OR in bot scripts that support many types to know which type to reconnect with
        public Dictionary<string, object> characterConfig;

        public string error;
        // ReSharper enable InconsistentNaming

        public RGServerHandshake(string token, Dictionary<string, object> characterConfig, string error)
        {
            this.token = token;
            this.characterConfig = characterConfig;
            this.error = error;
        }
    }
}
