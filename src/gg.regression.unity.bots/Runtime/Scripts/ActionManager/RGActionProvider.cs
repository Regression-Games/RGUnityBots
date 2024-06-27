using System;
using System.Collections.Generic;

namespace RegressionGames.ActionManager
{
    /// <summary>
    /// This interface provides the set of action types that were statically identified in the game.
    /// This must be usable both in-editor and at run time.
    /// </summary>
    public interface IRGActionProvider
    {
        /// <summary>
        /// Event that is broadcasted when this provider's actions change.
        /// </summary>
        public event EventHandler ActionsChanged;
        
        /// <summary>
        /// Provides the static set of all action types identified in the game.
        /// This is called once at the beginning of a bot/test session.
        /// </summary>
        public IEnumerable<RGGameAction> Actions { get; }
    }
}