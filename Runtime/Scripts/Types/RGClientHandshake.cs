using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGClientHandshake
    {
        public string unityToken;
        public string rgToken;
        public string botName;
        public string characterConfig;
        public bool spawnable;
        /**
            One of ...
            MANAGED - Server disconnects/ends bot on match/game-scene teardown
            PERSISTENT - Bot is responsible for disconnecting / ending itself
         */
        public string lifecycle;
    }
}

