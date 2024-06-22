using System;
using System.Collections.Generic;

namespace RegressionGames.ActionManager
{
    public interface IRGActionProvider
    {
        /// <summary>
        /// Event that is broadcasted when this provider's actions change.
        /// </summary>
        public event EventHandler ActionsChanged;
        
        /// <summary>
        /// Provides the set of all action types identified in the game.
        /// This may be called every frame, so it should be light-weight.
        /// 
        /// Any actual identification logic should take place during the construction
        /// of this provider.
        /// </summary>
        public IEnumerable<RGGameAction> Actions { get; }
    }
}