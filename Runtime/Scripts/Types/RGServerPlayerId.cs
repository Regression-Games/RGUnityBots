using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGServerPlayerId
    {
        // send the client their player object's Id
        public int playerId;

        public RGServerPlayerId( int playerId)
        {
            this.playerId = playerId;
        }
    }
}

