using System;
using System.Collections.Generic;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGQueueLobbiesRequest
    {
        public List<long> lobbyIds;
        public string gameEngineHost;
        public int gameEnginePort;
        public string externalAuthToken;

        public RGQueueLobbiesRequest(List<long> lobbyIds, string gameEngineHost, int gameEnginePort, string externalAuthToken)
        {
            this.lobbyIds = lobbyIds;
            this.gameEngineHost = gameEngineHost;
            this.gameEnginePort = gameEnginePort;
            this.externalAuthToken = externalAuthToken;
        }
    }
}

