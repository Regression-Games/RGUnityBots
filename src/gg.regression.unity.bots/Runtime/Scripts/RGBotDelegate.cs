using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RegressionGames
{
    /// <summary>
    /// A component that allows a GameObject to communicate back to the RGBot system.
    /// </summary>
    public class RGBotDelegate: MonoBehaviour
    {
        /// <summary>
        /// The ID of the bot that this delegate is associated with.
        /// </summary>
        internal long clientId;

        /// <summary>
        /// Called in the main loop to determine if this bot should trigger dynamic ticks.
        /// If this returns a non-zero value, then the bot will be ticked that many times.
        /// </summary>
        public virtual int GetDynamicTickCount()
        {
            return 0;
        }

        public virtual void UpdateState(Dictionary<string, IRGStateEntity> state)
        {
        }
    }
}
