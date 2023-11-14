using System.Linq;
using System.Threading.Tasks;
using RegressionGames;
using UnityEngine;

namespace RGThirdPersonDemo.RegressionGames
{
    public class BotGameLoop: MonoBehaviour
    {
        void Awake()
        {
            RGBotServerListener.GetInstance()?.StartGame();
            RGBotServerListener.GetInstance()?.SpawnBots();
            Debug.Log("Starting bot system");
        }

        private void OnDestroy()
        {
            RGBotServerListener.GetInstance()?.StopGame();
        }
    }
}