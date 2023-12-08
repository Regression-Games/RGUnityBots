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

        public virtual bool MustActivateThisFrame()
        {
            return false;
        }

        public virtual void UpdateState(Dictionary<string, IRGStateEntity> state)
        {
        }
    }
}
