using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.ActionManager
{
    /// <summary>
    /// Represents an individual property change of an action.
    /// This is used to store overrides for action properties in the settings.
    /// </summary>
    [Serializable]
    public class RGActionPropertySetting
    {
        public string propertyName;
        public string propertyValue;

        public RGActionPropertySetting(string propertyName, string propertyValue)
        {
            this.propertyName = propertyName;
            this.propertyValue = propertyValue;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"propertyName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, propertyName);
            stringBuilder.Append(",\"propertyValue\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, propertyValue);
            stringBuilder.Append("}");
        }
    }
    
    /// <summary>
    /// Stores the settings for the action manager.
    /// This is saved and loaded from an asset in RGActionManager.
    /// The settings are saved via RGActionManager.SaveSettings().
    /// </summary>
    [Serializable]
    public class RGActionManagerSettings
    {
        public const int SETTINGS_API_VERSION = 1; // Increment whenever breaking changes are made to the settings format

        // API version of the settings file
        public int apiVersion = SETTINGS_API_VERSION;
        
        // Set of actions that were disabled by the user
        public ISet<string> DisabledActionPaths = new HashSet<string>();

        // Set of action property changes to override the defaults determined by the analysis
        public Dictionary<string, List<RGActionPropertySetting>> ActionProperties =
            new Dictionary<string, List<RGActionPropertySetting>>();

        /// <summary>
        /// Apply the current user settings to the complete set of original actions.
        /// This returns a new set of actions that excludes any disabled actions and includes the property changes
        /// from the user settings.
        /// </summary>
        public List<RGGameAction> ApplySettings(IEnumerable<RGGameAction> originalActions)
        {
            List<RGGameAction> result = new List<RGGameAction>();
            foreach (var origAction in originalActions)
            {
                bool enabled = origAction.Paths.Any(IsActionEnabled);
                if (enabled)
                {
                    result.Add(ApplySettings(origAction));
                }
            }
            return result;
        }
        
        /// <summary>
        /// Apply the current user settings to a single action.
        /// Returns a cloned action with the updated settings.
        /// Note that this method does not consider whether the action is disabled.
        /// </summary>
        public RGGameAction ApplySettings(RGGameAction origAction)
        {
            // create a clone of the original action and apply any property changes in the settings
            var action = (RGGameAction)origAction.Clone();
            foreach (string[] path in action.Paths)
            {
                string pathStr = string.Join("/", path);
                if (ActionProperties.TryGetValue(pathStr, out List<RGActionPropertySetting> propSettings))
                {
                    foreach (var propSetting in propSettings)
                    {
                        var prop = RGActionProperty.FindProperty(action, propSetting.propertyName);
                        object value = prop.DeserializeValue(propSetting.propertyValue);
                        prop.SetValue(value);
                    }
                }
            }
            return action;
        }

        /// <summary>
        /// Store the current value of the property as an override in ActionProperties.
        /// </summary>
        public void StoreProperty(RGActionPropertyInstance prop)
        {
            string propertyName = prop.MemberInfo.Name;
            StringBuilder stringBuilder = new StringBuilder();
            prop.WriteValueToStringBuilder(stringBuilder);
            string propertyValue = stringBuilder.ToString();
            foreach (string[] path in prop.Action.Paths)
            {
                string pathStr = string.Join("/", path);
                if (!ActionProperties.TryGetValue(pathStr, out var propSettings))
                {
                    propSettings = new List<RGActionPropertySetting>();
                    ActionProperties.Add(pathStr, propSettings);
                }
                var propSetting = propSettings.FirstOrDefault(pc => pc.propertyName == propertyName);
                if (propSetting != null)
                {
                    propSetting.propertyValue = propertyValue;
                }
                else
                {
                    propSetting = new RGActionPropertySetting(propertyName, propertyValue);
                    propSettings.Add(propSetting);
                }
            }
        }

        /// <summary>
        /// Clear any overrides for the given property
        /// </summary>
        public void ResetProperty(RGActionPropertyInstance prop)
        {
            string propertyName = prop.MemberInfo.Name;
            foreach (string[] path in prop.Action.Paths)
            {
                string pathStr = string.Join("/", path);
                if (ActionProperties.TryGetValue(pathStr, out var propSettings))
                {
                    var propSetting = propSettings.FirstOrDefault(pc => pc.propertyName == propertyName);
                    if (propSetting != null)
                    {
                        propSettings.Remove(propSetting);
                        if (propSettings.Count == 0)
                        {
                            ActionProperties.Remove(pathStr);
                        }
                    }
                }
            }
        }

        public bool IsActionEnabled(string[] actionPath)
        {
            return !DisabledActionPaths.Contains(string.Join("/", actionPath));
        }

        public bool IsValid()
        {
            return apiVersion == SETTINGS_API_VERSION && DisabledActionPaths != null && ActionProperties != null;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n");
            stringBuilder.Append("\"DisabledActionPaths\":[\n");
            int disabledActionPathCount = DisabledActionPaths.Count;
            int disabledActionPathIndex = 0;
            foreach (string disabledActionPath in DisabledActionPaths)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, disabledActionPath);
                if (disabledActionPathIndex + 1 < disabledActionPathCount)
                {
                    stringBuilder.Append(",\n");
                }
                ++disabledActionPathIndex;
            }
            stringBuilder.Append("\n],\n");
            stringBuilder.Append("\"ActionProperties\":{\n");
            int actionPropCount = ActionProperties.Count;
            int actionPropIndex = 0;
            foreach (var actionPropEntry in ActionProperties)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, actionPropEntry.Key);
                stringBuilder.Append(":[");
                int propSettingsCount = actionPropEntry.Value.Count;
                for (int propSettingsIndex = 0; propSettingsIndex < propSettingsCount; ++propSettingsIndex)
                {
                    var propSetting = actionPropEntry.Value[propSettingsIndex];
                    propSetting.WriteToStringBuilder(stringBuilder);
                    if (propSettingsIndex + 1 < propSettingsCount)
                    {
                        stringBuilder.Append(",");
                    }
                }
                stringBuilder.Append("]");
                if (actionPropIndex + 1 < actionPropCount)
                {
                    stringBuilder.Append(",\n");
                }
                ++actionPropIndex;
            }
            stringBuilder.Append("\n}\n}");
        }
    }
}