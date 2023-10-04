using UnityEngine;

namespace RegressionGames.RGBotLocalRuntime
{
    // ReSharper disable InconsistentNaming
    public abstract class RGUserBot : ScriptableObject
    {
        // TODO (REG-1298): Solve how we want to persist these (maybe scriptable objects?) so that we can dynamically load
        // them into the runtime.
        // Open Question:  How do we index / know the available bots in the runtime easily... Maybe by scanning for assets implementing IRGUserBot ?

        // TODO (REG-1298): Is this pattern good enough to enforce users to implement it correctly ???
        //      But.. how do we handle botId and botName when they come from the server instead...
        //      We really need those to be metadata externally or part of the serializable asset itself ...
        public long botId => GetBotId();
        public string botName => GetBotName();
        public bool spawnable => GetIsSpawnable();
        public RGBotLifecycle lifecycle => GetBotLifecycle();

        protected abstract long GetBotId();
        protected abstract string GetBotName();
        protected abstract bool GetIsSpawnable();
        protected abstract RGBotLifecycle GetBotLifecycle();
        
        /**
         * <summary>Used to configure the user's bot before starting.</summary>
         * <param name="rgObject">{RG} Container object with access to character config, clientId, and state information</param>
         */
        public abstract void ConfigureBot(RG rgObject);

        /**
         * <summary>Used to process the current tick of the game state.</summary>
         * <param name="rgObject">{RG} Container object with access to character config, clientId, and state information</param>
         */
        public abstract void ProcessTick(RG rgObject);
    }
}