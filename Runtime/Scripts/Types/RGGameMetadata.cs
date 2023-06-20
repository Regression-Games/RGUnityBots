using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGGameMetadata
    {
        public int teamsPerMatch = 2;
        public int humansPerTeam = 0;
        public int playersPerTeam = 1;
        public bool startGameInstance = true;
    }
}

