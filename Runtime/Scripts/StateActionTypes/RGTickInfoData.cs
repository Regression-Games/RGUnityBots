using System;
using System.Collections.Generic;

namespace RegressionGames.StateActionTypes
{
    [Serializable]
    public class RGTickInfoData
    {
        public long tick;
        public string sceneName;
        public Dictionary<string, object> gameState;

        public RGTickInfoData(long t, string sceneName, Dictionary<string, object> gameState)
        {
            tick = t;
            this.sceneName = sceneName;
            this.gameState = gameState;
        }
    }
}
