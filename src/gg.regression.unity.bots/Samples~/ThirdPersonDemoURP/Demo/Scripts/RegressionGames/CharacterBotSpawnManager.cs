using RegressionGames;
using RegressionGames.Types;
using UnityEngine;

namespace RGThirdPersonDemo.RegressionGames
{
    public class CharacterBotSpawnManager : RGBotSpawnManager
    {
    
        [SerializeField]
        [Tooltip("The character to spawn")]
        private GameObject rgBotPrefab;

        [SerializeField]
        [Tooltip("Spawn point for RG Bots")]
        private Transform botSpawnPoint;

        public override GameObject SpawnBot(bool lateJoin, BotInformation botInformation)
        {
            var bot = Instantiate(rgBotPrefab, Vector3.zero, Quaternion.identity);
            bot.transform.position = botSpawnPoint.position;

            // We will add more code here later to configure the bot further 
            return bot;
        }

    }
}