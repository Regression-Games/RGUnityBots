using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGEvent
    {
        public long id;
        public string gameMode;
        public string title;
        public RGGameMetadata gameMetadata;
    }
}

