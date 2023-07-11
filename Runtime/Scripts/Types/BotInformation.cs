using System;
using Unity.Plastic.Newtonsoft.Json;
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
         * <summary>
         * Parses the JSON from characterConfig into the serialized data type
         * passed into the generic of this function.
         * </summary>
         * <returns>An object of type T deserialized from the JSON string</returns>
         * <example>
         * <code>
         * [Serializable]
         * public class BotCharacterConfig
         * {
         *     public float speed;
         * }
         * var myBotConfig = botInformation.ParseCharacterConfig&lt;BotCharacterConfig&gt;();
         * Debug.Log(myBotConfig.speed);
         * </code>
         * </example>
         */
        public T ParseCharacterConfig<T>()
        {
            return JsonConvert.DeserializeObject<T>(characterConfig);
        }

        /**
         * <summary>
         * Updates the bots character config - this is useful for overriding or adding new
         * information defined and set by your Unity code. For example, when seating a bot, you may
         * discover that the requested character type is no longer available, and you need to let
         * the bot know. The generic type you pass in must be [Serializable].
         * </summary>
         * <param name="newConfig">The new config to save and send to the bot</param>
         * <example>
         * <code>
         * [Serializable]
         * public class BotCharacterConfig
         * {
         *     public float speed;
         * }
         * var newConfig = BotCharacterConfig()
         * newConfig.speed = 1000;
         * var myBotConfig = botInformation.UpdateCharacterConfig&lt;BotCharacterConfig&gt;(newConfig);
         * </code>
         * </example>
         */
        public void UpdateCharacterConfig<T>(T newConfig)
        {
            characterConfig = JsonConvert.SerializeObject(newConfig);
        }
        
    }
}