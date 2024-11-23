using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.ActionManager;
using RegressionGames.GenericBots.Experimental.Rewards;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RegressionGames.GenericBots.Experimental
{

    /// <summary>
    /// This bot implements the Q-learning reinforcement learning algorithm.
    /// It uses the Action Manager to automatically compute a discrete action space for the game,
    /// and uses a generic state representation based on the component types present in the scene.
    ///
    /// By default, the bot aims to explore a variety of functionality via a generic exploration reward.
    /// A custom reward can be specified by overriding the CreateRewardModule() method.
    ///
    /// When the bot is first started, it records the initial scene it was started in. The bot runs an episode for a
    /// fixed amount of time defined by EpisodeDuration, then restarts the game by invoking SceneManager.LoadScene
    /// on the initial scene it was loaded with. The logic for restarting the game can be modified by overriding the RestartGame() method.
    ///
    /// At the end of each episode, the model (Q-table) is saved to ModelFilePath,
    /// as well as the training state (epsilon value). The random action selection rate (epsilon)
    /// decays after each episode by the amount specified in EpsilonDecayPerEpisode.
    ///
    /// To use this bot, create a MonoBehaviour in your project that inherits from this bot.
    /// Have it inherit from the IRGBot interface so it appears in the Regression Games Bot Manager.
    /// <code>
    /// class MyGameQLearningBot : QLearningBot, IRGBot
    /// {
    ///     protected override IRewardModule CreateRewardModule() { /* define your own reward if desired */ }
    /// }
    /// </code>
    /// </summary>
    public class QLearningBot : MonoBehaviour
    {

        public bool Training = true; // if disabled, the Q-table is frozen and the game runs at normal speed
        public float ActionInterval = 0.05f; // Interval on which to take actions, expressed in scaled time (seconds). If zero, actions taken every frame.
        public float EpisodeDuration = 30.0f; // Duration of the episode, in scaled time (seconds)
        public float EpsilonDecayPerEpisode = 0.95f; // Exponential decay factor of epsilon during training after each episode
        public float MinEpsilon = 0.05f; // The lowest epsilon value possible
        public float Alpha = 0.001f; // Learning rate
        public float Gamma = 0.6f; // Discount factor
        public int ExperienceBufferSize = 64; // Size of the experience buffer (size of fixed-length queue of the last N experiences)
        public string ModelFilePath = "qbot_model.json"; // Path where to save the trained model (Q-table)
        public float TrainingTimeScale = 3.0f; // the Time.timeScale value to use while training

        private QLearningBotLogic botLogic;

        private string _initialSceneName;

        public void Start()
        {
            if (!RGActionManager.IsAvailable)
            {
                RGDebug.LogError("Action manager is currently unavailable. Have you run Regression Games > Configure Bot Actions > Analyze Actions on your project?");
                Destroy(this);
                return;
            }
            DontDestroyOnLoad(this);

            RGActionRuntimeCoverageAnalysis.Reset();

            botLogic = new QLearningBotLogic()
            {
                Training = Training,
                ActionInterval = ActionInterval,
                EpisodeDuration = EpisodeDuration,
                EpsilonDecayPerEpisode = EpsilonDecayPerEpisode,
                MinEpsilon = MinEpsilon,
                Gamma = Gamma,
                Alpha = Alpha,
                ExperienceBufferSize = ExperienceBufferSize,
                ModelFilePath = ModelFilePath,
                TrainingTimeScale = TrainingTimeScale,
                RewardModule = CreateRewardModule()
            };

            _initialSceneName = SceneManager.GetActiveScene().name;

            try
            {
                botLogic.Start();
                botLogic.OnEpisodeStart(0, this);
            }
            catch (Exception)
            {
                // failed to load.. clean this up
                Object.Destroy(this);
            }
        }

        public void OnDestroy()
        {
            botLogic?.OnDestroy();
            RGActionManager.StopSession();
        }

        public void Update()
        {
            if (botLogic != null)
            {
                botLogic.ActionInterval = ActionInterval;
                botLogic.Update(0, this, _initialSceneName);
            }
        }

        /// <summary>
        /// Create a reward module. This defaults to exploration reward.
        /// Game-specific bots can override this to provide a custom reward.
        /// </summary>
        protected virtual IRewardModule CreateRewardModule()
        {
            return new CameraPositionRewardModule();
        }
    }
}
