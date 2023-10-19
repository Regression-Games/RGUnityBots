using System;
using UnityEngine;

namespace RegressionGames.RGBotLocalRuntime
{
    // ReSharper disable InconsistentNaming
    public abstract class RGUserBot : ScriptableObject
    {
        public bool spawnable => GetIsSpawnable();
        public RGBotLifecycle lifecycle => GetBotLifecycle();

        public long BotId => _botId;
        public string BotName => _botName;

        private long _botId;
        private string _botName;

        public void Init(long botId, string botName)
        {
            _botId = botId;
            _botName = botName;
        }
        
        protected abstract bool GetIsSpawnable();
        protected abstract RGBotLifecycle GetBotLifecycle();
        
        /**
         * <summary>Used to configure the user's bot before starting. ALWAYS call base.ConfigureBot(rgObject) to help setup key information about your bot.</summary>
         * <param name="rgObject">{RG} Container object with access to character config, clientId, and state information</param>
         */
        public abstract void ConfigureBot(RG rgObject);

        /**
         * <summary>Used to process the current tick of the game state.</summary>
         * <param name="rgObject">{RG} Container object with access to character config, clientId, and state information</param>
         */
        public abstract void ProcessTick(RG rgObject);

        public virtual void OnDrawGizmos()
        {
        }
    }
}