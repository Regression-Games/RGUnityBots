using System;
using System.Collections.Generic;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGClientHandshake
    {
        // ReSharper disable InconsistentNaming
        public string unityToken;
        public string rgToken;
        public string botName;
        public Dictionary<string, object> characterConfig;
        public bool spawnable;

        /**
            One of ...
            MANAGED - Server disconnects/ends bot on match/game-scene teardown
            PERSISTENT - Bot is responsible for disconnecting / ending itself
         */
        public string lifecycle;
        // ReSharper enable InconsistentNaming
    }
}
