using System;

namespace RegressionGames.GenericBots.Experimental
{
    /// <summary>
    /// Module that calculates the agent reward for the last action taken.
    /// </summary>
    public interface IRewardModule : IDisposable
    {
        /// <summary>
        /// Returns the reward for the last action performed.
        /// </summary>
        public float GetRewardForLastAction();
        
        /// <summary>
        /// Resets any state being tracked by the reward module.
        /// </summary>
        public void Reset();
    }
}