using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Composites;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action to simulate inputs for a given InputAction from the new Input System.
    /// Currently this only supports keyboard/mouse bindings.
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

        private static int? FindKeyboardMouseBinding(InputAction action)
        {
            bool IsKeyboardMouseBinding(InputBinding binding)
            {
                var control = InputSystem.FindControl(binding.path);
                return control != null && control.device is Keyboard or Mouse;
            }
            int bindingsCount = action.bindings.Count;
            for (int bindingIndex = 0; bindingIndex < bindingsCount; ++bindingIndex)
            {
                var binding = action.bindings[bindingIndex];
                if (binding.isComposite)
                {
                    for (int childBindingIndex = bindingIndex + 1; childBindingIndex < bindingsCount; ++childBindingIndex)
                    {
                        var childBinding = action.bindings[childBindingIndex];
                        if (!childBinding.isPartOfComposite)
                            break;
                        if (IsKeyboardMouseBinding(childBinding))
                        {
                            return bindingIndex;
                        }
                    }
                }
                else if (IsKeyboardMouseBinding(binding))
                {
                    return bindingIndex;
                }
            }
            return null;
        }

        /// <summary>
        /// Figures out the range of the action by examining
        /// the first keyboard/mouse binding of the action.
        /// </summary>
        private static IRGValueRange GetDefaultParamRange(InputAction action)
        {
            IRGValueRange ParamRangeFromBinding(InputBinding binding)
            {
                Debug.Assert(!binding.isComposite);
                var control = InputSystem.FindControl(binding.path);
                if (control == null)
                    return null;

                if (control is ButtonControl)
                {
                    return new RGBoolRange();
                } else if (control is Vector2Control)
                {
                    return new RGVector2IntRange(new Vector2Int(-1, -1), new Vector2Int(1, 1));
                } else if (control is Vector3Control)
                {
                    return new RGVector3IntRange(new Vector3Int(-1, -1, -1), new Vector3Int(1, 1, 1));
                } else if (control is AxisControl)
                {
                    return new RGIntRange(-1, 1);
                }
                else
                {
                    RGDebug.LogWarning($"Unsupported control type {control.GetType()} ({control})");
                }
                return null;
            }

            int? keybdMouseBindingIndex = FindKeyboardMouseBinding(action);
            if (keybdMouseBindingIndex.HasValue)
            {
                var binding = action.bindings[keybdMouseBindingIndex.Value];
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
                        return new RGVector3IntRange(new Vector3Int(-1, -1, -1), new Vector3Int(1, 1, 1));
                    } else if (typeof(ButtonWithOneModifier).IsAssignableFrom(compositeType))
                    {
                        return new RGBoolRange();
                    } else if (typeof(ButtonWithTwoModifiers).IsAssignableFrom(compositeType))
                    {
                        return new RGBoolRange();
                    } else if (typeof(OneModifierComposite).IsAssignableFrom(compositeType))
                    {
                        if (TryFindCompositePartBinding(action, keybdMouseBindingIndex.Value, "binding", out InputBinding childBinding))
                        {
                            return ParamRangeFromBinding(childBinding);
                        }
                    } else if (typeof(TwoModifiersComposite).IsAssignableFrom(compositeType))
                    {
                        if (TryFindCompositePartBinding(action, keybdMouseBindingIndex.Value, "binding", out InputBinding childBinding))
                        {
                            return ParamRangeFromBinding(childBinding);
                        }
                    }
                }
                else
                {
                    return ParamRangeFromBinding(binding);
                }
            }

            return null;
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