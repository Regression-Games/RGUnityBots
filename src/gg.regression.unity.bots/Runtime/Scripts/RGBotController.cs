using System;
using RegressionGames.Types;
using UnityEngine;

namespace RegressionGames
{
    /// <summary>
    /// A component that will be injected in to the game when a bot is spawned, allowing it to interact with the game.
    /// </summary>
    public abstract class RGBotController: MonoBehaviour
    {
        /// <summary>
        /// The ID of the bot that this delegate is associated with.
        /// </summary>
        internal long clientId;

        private RGBotInstance _botInstance;

        public RGBotInstance BotInstance => _botInstance;

        internal void SetBotInstance(RGBotInstance instance)
        {
            _botInstance = instance;
        }
    }
}
