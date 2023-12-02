using UnityEngine;

namespace RegressionGames
{
    public class RGRuntimeProperties
    {

        
        public const string NEXT_BOT_INSTANCE_ID = "RGNextBotInstanceId";

        public static long GetNextBotInstanceId()
        {
            var systemId = RGSettings.GetSystemId();
            var nextId = PlayerPrefs.GetInt(NEXT_BOT_INSTANCE_ID, 0) + 1;

            PlayerPrefs.SetInt(NEXT_BOT_INSTANCE_ID, nextId);
            
            // this is so that 'to a human' these ids look sequential
            if (systemId < 0)
            {
                systemId -= nextId;
            }
            else
            {
                systemId += nextId;
            }
            return systemId;
        }
    }
    
}
