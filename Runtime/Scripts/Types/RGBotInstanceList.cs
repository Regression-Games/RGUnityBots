using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGBotInstanceList
    {
        public RGBotInstance[] botInstances;
        
        public override string ToString()
        {
            return string.Join<RGBotInstance>(",", botInstances);
        }
    }
}

