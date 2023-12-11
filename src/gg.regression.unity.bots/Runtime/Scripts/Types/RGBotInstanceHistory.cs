using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGBotInstanceHistory
    {
        public long id;
        public RGBot bot;
        public long botInstanceId;
        public long runtimeDuration;
        public string validationRunSummary;
    }
}