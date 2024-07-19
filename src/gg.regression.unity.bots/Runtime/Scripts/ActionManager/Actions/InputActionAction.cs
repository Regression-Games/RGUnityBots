using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action to simulate inputs for a given InputAction from the new Input System.
    /// </summary>
    public class InputActionAction : RGGameAction
    {
        public string ActionAssetName { get; }
        public RGActionParamFunc<InputAction> EmbeddedActionFunc { get; }
        public string ActionName { get; }
        public Guid ActionGuid { get; }

        /// <summary>
        /// Constructor for embedded InputActions
        /// </summary>
        public InputActionAction(string[] path, Type embeddedObjectType, RGActionParamFunc<InputAction> embeddedActionFunc, InputAction action) : 
            base(path, embeddedObjectType, GetDefaultParamRange(action))
        {
            ActionAssetName = null;
            EmbeddedActionFunc = embeddedActionFunc;
            ActionName = action.name;
            ActionGuid = action.id;
        }

        /// <summary>
        /// Constructor for InputActions within InputActionAssets
        /// </summary>
        public InputActionAction(string[] path, InputAction action) : 
            base(path, typeof(InputActionAsset), GetDefaultParamRange(action))
        {
            ActionAssetName = action.actionMap.asset.name;
            EmbeddedActionFunc = null;
            ActionName = action.name;
            ActionGuid = action.id;
        }

        public InputActionAction(JObject serializedAction) : base(serializedAction)
        {
            var actionAssetName = serializedAction["actionAssetName"];
            if (actionAssetName.Type != JTokenType.Null)
            {
                ActionAssetName = actionAssetName.ToString();
            }

            var embeddedActionFunc = serializedAction["embeddedActionFunc"];
            if (embeddedActionFunc.Type != JTokenType.Null)
            {
                EmbeddedActionFunc = RGActionParamFunc<InputAction>.Deserialize(embeddedActionFunc);
            }

            ActionName = serializedAction["actionName"].ToString();
            ActionGuid = Guid.Parse(serializedAction["actionGuid"].ToString());
        }

        private static IRGValueRange GetDefaultParamRange(InputAction action)
        {
            // TODO
            return new RGBoolRange();
        }

        public override string DisplayName => $"InputAction {ActionName}";

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is InputActionAction inputActionAction)
            {
                return ActionAssetName == inputActionAction.ActionAssetName
                       && EmbeddedActionFunc == inputActionAction.EmbeddedActionFunc
                       && ActionName == inputActionAction.ActionName
                       && ActionGuid == inputActionAction.ActionGuid;
            }
            else
            {
                return false;
            }
        }
        
        public override bool IsValidForObject(Object obj)
        {
            throw new NotImplementedException();
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            throw new NotImplementedException();
        }

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append(",\n\"actionAssetName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, ActionAssetName);
            stringBuilder.Append(",\n\"embeddedActionFunc\":");
            if (EmbeddedActionFunc != null)
            {
                EmbeddedActionFunc.WriteToStringBuilder(stringBuilder);
            }
            else
            {
                stringBuilder.Append("null");
            }
            stringBuilder.Append(",\n\"actionName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, ActionName);
            stringBuilder.Append(",\n\"actionGuid\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, ActionGuid.ToString());
        }

        public override IEnumerable<(string, string)> GetDisplayActionAttributes()
        {
            foreach (var attr in base.GetDisplayActionAttributes())
                yield return attr;

            if (ActionAssetName != null)
            {
                yield return ("Action Asset Name", ActionAssetName);
            }

            if (EmbeddedActionFunc != null)
            {
                yield return ("Embedded Action Source", EmbeddedActionFunc.ToString());
            }

            yield return ("Action Name", ActionName);
            yield return ("Action GUID", ActionGuid.ToString());
        }
    }
}