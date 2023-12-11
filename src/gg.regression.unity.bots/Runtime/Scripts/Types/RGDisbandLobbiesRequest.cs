using System;
using System.Collections.Generic;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGDisbandLobbiesRequest
    {
        public List<long> lobbyIds;


        public RGDisbandLobbiesRequest(List<long> lobbyIds)
        {
            this.lobbyIds = lobbyIds;

        }
    }
}

