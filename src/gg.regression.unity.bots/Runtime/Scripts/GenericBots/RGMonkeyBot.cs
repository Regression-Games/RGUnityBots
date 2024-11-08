using RegressionGames.ActionManager;
using UnityEngine;

namespace RegressionGames.GenericBots
{
    public class RGMonkeyBot : MonoBehaviour, IRGBot
    {
        public float actionInterval = 0.05f; // unscaled time
        private RGMonkeyBotLogic monkey;

        void Start()
        {
            if (!RGActionManager.IsAvailable)
            {
                RGDebug.LogError("Monkey bot is currently unavailable. Have you run Regression Games > Configure Bot Actions > Analyze Actions on your project?");
                Destroy(this);
                return;
            }
            RGActionManager.StartSession(0, this);

            monkey = new RGMonkeyBotLogic();
            DontDestroyOnLoad(this);
        }

        void Update()
        {
            monkey.ActionInterval = actionInterval;
            monkey.Update(0);
        }

        void OnDestroy()
        {
            RGActionManager.StopSession();
        }
    }
}
