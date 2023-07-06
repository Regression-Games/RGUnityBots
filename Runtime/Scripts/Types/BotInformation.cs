using System;
using UnityEngine;

namespace RegressionGames.Types
{
    [Serializable]
    public class BotInformation
    {
        public uint clientId;
        public string botName;
        public string characterConfig;

        public BotInformation(uint clientId, string botName, string characterConfig)
        {
            this.clientId = clientId;
            this.botName = botName;
            this.characterConfig = characterConfig;
        }

        /**
         * Parses the JSON from characterConfig into the serialized data type
         * passed into the generic of this function.
         */
        public T ParseCharacterConfig<T>()
        {
            return JsonUtility.FromJson<T>(characterConfig);
        }
        
    }
}