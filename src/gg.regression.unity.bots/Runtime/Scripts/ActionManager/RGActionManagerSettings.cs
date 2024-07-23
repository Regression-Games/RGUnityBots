using System;
using System.Collections.Generic;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.ActionManager
{
    /// <summary>
    /// Stores the settings for the action manager.
    /// This is saved and loaded from an asset in RGActionManager.
    /// The settings are saved via RGActionManager.SaveSettings().
    /// </summary>
    [Serializable]
    public class RGActionManagerSettings
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

        public bool IsValid()
        {
            return DisabledActionPaths != null;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n");
            stringBuilder.Append("\"DisabledActionPaths\":[\n");
            int disabledActionPathCount = DisabledActionPaths.Count;
            for (int i = 0; i < disabledActionPathCount; ++i)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, DisabledActionPaths[i]);
                if (i + 1 < disabledActionPathCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]");
            stringBuilder.Append("\n}");
        }
    }
}