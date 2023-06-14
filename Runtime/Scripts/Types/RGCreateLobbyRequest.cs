using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGCreateLobbyRequest
    {
        public string matchType;
        public long eventId;
        public bool addLeaderAsPlayer = false;

        public RGCreateLobbyRequest(string matchType, long eventId)
        {
            this.matchType = matchType;
            this.eventId = eventId;
        }
    }
}

