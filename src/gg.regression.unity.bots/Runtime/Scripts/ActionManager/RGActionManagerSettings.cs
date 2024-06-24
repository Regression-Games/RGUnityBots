using System;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.ActionManager
{
    /// <summary>
    /// Stores the settings for the action manager.
    /// This is saved and loaded from an asset in RGActionManager.
    /// The settings are saved via RGActionManager.SaveSettings().
    /// </summary>
    public class RGActionManagerSettings : ScriptableObject
    {
        // Whenever modifying any serializable fields, call MarkDirty()
        
        public List<string> DisabledActionPaths = new List<string>();

        [NonSerialized]
        private ISet<string> _disabledActionPathSet;

        public bool IsActionEnabled(string[] actionPath)
        {
            if (_disabledActionPathSet == null)
            {
                _disabledActionPathSet = new HashSet<string>(DisabledActionPaths);
            }
            return !_disabledActionPathSet.Contains(string.Join("/", actionPath));
        }

        public void MarkDirty()
        {
            _disabledActionPathSet = null;
        }
    }
}