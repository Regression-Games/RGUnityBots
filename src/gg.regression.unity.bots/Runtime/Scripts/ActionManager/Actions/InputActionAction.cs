using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Composites;
using UnityEngine.InputSystem.Utilities;
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

        private static bool TryFindCompositePartBinding(InputAction action, int compositeBindingIndex, string partName, out InputBinding result)
        {
            for (int partIndex = compositeBindingIndex + 1; partIndex < action.bindings.Count; ++partIndex)
            {
                var binding = action.bindings[partIndex];
                if (binding.isPartOfComposite)
                {
                    if (binding.name == partName)
                    {
                        result = binding;
                        return true;
                    }
                }
            }
            result = new InputBinding();
            return false;
        }

        private static IRGValueRange GetDefaultParamRange(InputAction action)
        {
            IRGValueRange ParamRangeFromBinding(InputBinding binding)
            {
                // TODO
                return new RGBoolRange();
            }

            for (int bindingIndex = 0; bindingIndex < action.bindings.Count; ++bindingIndex)
            {
                var binding = action.bindings[bindingIndex];
                if (binding.isComposite)
                {
                    string compositeName = binding.GetNameOfComposite();
                    Type compositeType = InputSystem.TryGetBindingComposite(compositeName);
                    if (compositeType == null)
                    {
                        RGDebug.LogWarning($"Unable to resolve composite \"{compositeName}\"");
                        return null;
                    }
                    if (typeof(AxisComposite).IsAssignableFrom(compositeType))
                    {
                        return new RGIntRange(-1, 1); // discretized axis range (equal chance of negative, zero, positive)
                    } else if (typeof(Vector2Composite).IsAssignableFrom(compositeType))
                    {
                        return new RGVector2IntRange(new Vector2Int(-1, -1), new Vector2Int(1, 1));
                    } else if (typeof(Vector3Composite).IsAssignableFrom(compositeType))
                    {
                        
                    } else if (typeof(ButtonWithOneModifier).IsAssignableFrom(compositeType))
                    {
                        
                    } else if (typeof(ButtonWithTwoModifiers).IsAssignableFrom(compositeType))
                    {
                        
                    } else if (typeof(OneModifierComposite).IsAssignableFrom(compositeType))
                    {
                        
                    } else if (typeof(TwoModifiersComposite).IsAssignableFrom(compositeType))
                    {
                        
                    }
                }
                else
                {
                    
                }
            }
            
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