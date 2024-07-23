using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Composites;
using UnityEngine.InputSystem.Controls;
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
        
        public float MouseMovementMagnitude = 10.0f; // for axes that read mouse delta, the amount to move/scroll the mouse

        /// <summary>
        /// Constructor for embedded InputActions
        /// </summary>
        public InputActionAction(string[] path, Type embeddedObjectType, RGActionParamFunc<InputAction> embeddedActionFunc, InputAction action) : 
            base(path, embeddedObjectType, GetDefaultParamRange(action))
        {
            ActionAssetName = null;
            EmbeddedActionFunc = embeddedActionFunc;
            ActionName = action.name;
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

        public static bool TryFindCompositePartBinding(InputAction action, int compositeBindingIndex, string partName, out InputBinding result)
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
                else
                {
                    break;
                }
            }
            result = new InputBinding();
            return false;
        }

        public static int? FindKeyboardMouseBinding(InputAction action)
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
                else if (!binding.isPartOfComposite && IsKeyboardMouseBinding(binding))
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

        public InputAction GetActionFromObject(Object obj)
        {
            InputAction action = null;
            if (EmbeddedActionFunc != null)
            {
                action = EmbeddedActionFunc.Invoke(obj);
            }
            else
            {
                InputActionAsset asset = (InputActionAsset)obj;
                if (asset.name == ActionAssetName && asset.enabled)
                {
                    action = asset.FindAction(ActionGuid);
                }
            }
            return action;
        }
        
        public override bool IsValidForObject(Object obj)
        {
            InputAction action = GetActionFromObject(obj);
            return action != null && action.enabled;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new InputActionInstance(this, obj);
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

            yield return ("Action Name", ActionName);
            
            if (EmbeddedActionFunc != null)
            {
                yield return ("Embedded Action Source", EmbeddedActionFunc.ToString());
            }
            else
            {
                yield return ("Action GUID", ActionGuid.ToString());
            }
        }
    }

    public class InputActionInstance : RGGameActionInstance<InputActionAction, object>
    {
        public InputActionInstance(InputActionAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override bool IsValidActionParameter(object param)
        {
            return true;
        }

        private IEnumerable<RGActionInput> GetInputsForBinding(InputBinding binding, object param)
        {
            Debug.Assert(!binding.isComposite); // this should only be given non-composite bindings (either standalone or part of a composite)
            InputControl control = InputSystem.FindControl(binding.path);
            bool recognized = false;
            if (control != null)
            {
                if (control is ButtonControl buttonControl)
                {
                    bool paramVal = (bool)param;
                    if (buttonControl.device is Keyboard)
                    {
                        if (buttonControl is KeyControl keyControl)
                        {
                            yield return new InputSystemKeyInput(keyControl.keyCode, paramVal);
                            recognized = true;
                        }
                        else if (binding.path.EndsWith("/shiftKey"))
                        {
                            yield return new InputSystemKeyInput(Key.LeftShift, paramVal);
                            recognized = true;
                        } else if (binding.path.EndsWith("/ctrlKey"))
                        {
                            yield return new InputSystemKeyInput(Key.LeftCtrl, paramVal);
                            recognized = true;
                        } else if (binding.path.EndsWith("/altKey"))
                        {
                            yield return new InputSystemKeyInput(Key.LeftAlt, paramVal);
                            recognized = true;
                        } else if (binding.path.EndsWith("/anyKey"))
                        {
                            if (paramVal)
                            {
                                // press some key (Enter)
                                yield return new InputSystemKeyInput(Key.Enter, true);
                            }
                            else
                            {
                                // release all keys
                                foreach (Key key in Enum.GetValues(typeof(Key)))
                                {
                                    yield return new InputSystemKeyInput(key, false);
                                }
                            }
                            recognized = true;
                        }
                    }
                    else if (buttonControl.device is Mouse)
                    {
                        int? mouseButton = null;
                        if (binding.path.EndsWith("/leftButton") || binding.path.EndsWith("/press"))
                        {
                            mouseButton = MouseButtonInput.LEFT_MOUSE_BUTTON;
                        } else if (binding.path.EndsWith("/middleButton"))
                        {
                            mouseButton = MouseButtonInput.MIDDLE_MOUSE_BUTTON;
                        } else if (binding.path.EndsWith("/rightButton"))
                        {
                            mouseButton = MouseButtonInput.RIGHT_MOUSE_BUTTON;
                        } else if (binding.path.EndsWith("/forwardButton"))
                        {
                            mouseButton = MouseButtonInput.FORWARD_MOUSE_BUTTON;
                        } else if (binding.path.EndsWith("/backButton"))
                        {
                            mouseButton = MouseButtonInput.BACK_MOUSE_BUTTON;
                        }
                        if (mouseButton.HasValue)
                        {
                            yield return new MouseButtonInput(mouseButton.Value, paramVal);
                            recognized = true;
                        }
                    }
                } else if (control is Vector2Control v2Control)
                {
                    Vector2Int paramVal = (Vector2Int)param;
                    if (v2Control.device is Mouse)
                    {
                        if (binding.path.EndsWith("/position"))
                        {
                            Vector2 mousePos = new Vector2(Screen.width * (paramVal.x + 1.0f)/2.0f, Screen.height * (paramVal.y + 1.0f)/2.0f);
                            yield return new MousePositionInput(mousePos);
                            recognized = true;
                        } else if (binding.path.EndsWith("/delta"))
                        {
                            Vector2 mouseDelta = ((Vector2)paramVal) * Action.MouseMovementMagnitude;
                            yield return new MousePositionDeltaInput(mouseDelta);
                            recognized = true;
                        } else if (binding.path.EndsWith("/scroll"))
                        {
                            Vector2 mouseScroll = paramVal;
                            yield return new MouseScrollInput(mouseScroll);
                            recognized = true;
                        }
                    }
                }
                if (!recognized)
                {
                    RGDebug.LogWarning("Unsupported control " + control);
                }
            }
            else
            {
                RGDebug.LogWarning($"Missing control " + binding.path);
            }
        }

        private bool IsZeroInputParam(object param)
        {
            if (param is bool boolVal)
            {
                return boolVal == false;
            } else if (param is int intVal)
            {
                return intVal == 0;
            } else if (param is Vector2Int v2IntVal)
            {
                return v2IntVal == Vector2Int.zero;
            } else if (param is Vector3Int v3IntVal)
            {
                return v3IntVal == Vector3Int.zero;
            }
            else
            {
                throw new ArgumentException("Unexpected parameter " + param);
            }
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(object param)
        {
            InputAction inputAction = Action.GetActionFromObject(TargetObject);
            int? bindingIndex = InputActionAction.FindKeyboardMouseBinding(inputAction);
            if (bindingIndex.HasValue)
            {
                var binding = inputAction.bindings[bindingIndex.Value];
                if (binding.isComposite)
                {
                    string compositeName = binding.GetNameOfComposite();
                    Type compositeType = InputSystem.TryGetBindingComposite(compositeName);
                    if (typeof(AxisComposite).IsAssignableFrom(compositeType))
                    {
                        // Parameter range RGIntRange
                        int paramVal = (int)param;
                        if (InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "negative", out InputBinding negative)
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "positive", out InputBinding positive))
                        {
                            if (paramVal > 0)
                            {
                                foreach (var inp in GetInputsForBinding(positive, true))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(negative, false))
                                    yield return inp;
                            } else if (paramVal < 0)
                            {
                                foreach (var inp in GetInputsForBinding(positive, false))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(negative, true))
                                    yield return inp;
                            }
                            else // paramVal == 0
                            {
                                foreach (var inp in GetInputsForBinding(positive, false))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(negative, false))
                                    yield return inp;
                            }
                        }
                    } else if (typeof(Vector2Composite).IsAssignableFrom(compositeType))
                    {
                        // Parameter range RGVector2IntRange
                        Vector2Int paramVal = (Vector2Int)param;
                        if (InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "up", out InputBinding up)
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "down", out InputBinding down)
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "left", out InputBinding left)
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "right", out InputBinding right))
                        {
                            if (paramVal.x > 0)
                            {
                                foreach (var inp in GetInputsForBinding(right, true))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(left, false))
                                    yield return inp;
                            } else if (paramVal.x < 0)
                            {
                                foreach (var inp in GetInputsForBinding(right, false))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(left, true))
                                    yield return inp;
                            }
                            else // paramVal.x == 0
                            {
                                foreach (var inp in GetInputsForBinding(right, false))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(left, false))
                                    yield return inp;
                            }
                            
                            if (paramVal.y > 0)
                            {
                                foreach (var inp in GetInputsForBinding(up, true))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(down, false))
                                    yield return inp;
                            } else if (paramVal.y < 0)
                            {
                                foreach (var inp in GetInputsForBinding(up, false))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(down, true))
                                    yield return inp;
                            }
                            else // paramVal.y == 0
                            {
                                foreach (var inp in GetInputsForBinding(up, false))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(down, false))
                                    yield return inp;
                            }
                        }
                    } else if (typeof(Vector3Composite).IsAssignableFrom(compositeType))
                    {
                        // Parameter range RGVector3IntRange
                        Vector3Int paramVal = (Vector3Int)param;
                        if (InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "up", out InputBinding up)
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "down", out InputBinding down) 
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "left", out InputBinding left) 
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "right", out InputBinding right) 
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "forward", out InputBinding forward) 
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "backward", out InputBinding backward) )
                        {
                            if (paramVal.x > 0)
                            {
                                foreach (var inp in GetInputsForBinding(right, true))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(left, false))
                                    yield return inp;
                            } else if (paramVal.x < 0)
                            {
                                foreach (var inp in GetInputsForBinding(right, false))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(left, true))
                                    yield return inp;
                            }
                            else // paramVal.x == 0
                            {
                                foreach (var inp in GetInputsForBinding(right, false))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(left, false))
                                    yield return inp;
                            }
                            
                            if (paramVal.y > 0)
                            {
                                foreach (var inp in GetInputsForBinding(up, true))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(down, false))
                                    yield return inp;
                            } else if (paramVal.y < 0)
                            {
                                foreach (var inp in GetInputsForBinding(up, false))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(down, true))
                                    yield return inp;
                            }
                            else // paramVal.y == 0
                            {
                                foreach (var inp in GetInputsForBinding(up, false))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(down, false))
                                    yield return inp;
                            }
                            
                            if (paramVal.z > 0)
                            {
                                foreach (var inp in GetInputsForBinding(forward, true))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(backward, false))
                                    yield return inp;
                            } else if (paramVal.z < 0)
                            {
                                foreach (var inp in GetInputsForBinding(forward, false))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(backward, true))
                                    yield return inp;
                            }
                            else // paramVal.z == 0
                            {
                                foreach (var inp in GetInputsForBinding(forward, false))
                                    yield return inp;
                                foreach (var inp in GetInputsForBinding(backward, false))
                                    yield return inp;
                            }
                        }
                    } else if (typeof(ButtonWithOneModifier).IsAssignableFrom(compositeType))
                    {
                        // Parameter range RGBoolRange
                        if (InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "modifier", out InputBinding modifier)
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "button", out InputBinding button))
                        {
                            bool paramVal = (bool)param;
                            foreach (var inp in GetInputsForBinding(modifier, paramVal))
                            {
                                yield return inp;
                            }
                            foreach (var inp in GetInputsForBinding(button, paramVal))
                            {
                                yield return inp;
                            }
                        }
                    } else if (typeof(ButtonWithTwoModifiers).IsAssignableFrom(compositeType))
                    {
                        // Parameter range RGBoolRange
                        if (InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "modifier1",
                                out InputBinding modifier1)
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value,
                                "modifier2", out InputBinding modifier2)
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "button",
                                out InputBinding button))
                        {
                            bool paramVal = (bool)param;
                            foreach (var inp in GetInputsForBinding(modifier1, paramVal))
                            {
                                yield return inp;
                            }
                            foreach (var inp in GetInputsForBinding(modifier2, paramVal))
                            {
                                yield return inp;
                            }
                            foreach (var inp in GetInputsForBinding(button, paramVal))
                            {
                                yield return inp;
                            }
                        }
                    } else if (typeof(OneModifierComposite).IsAssignableFrom(compositeType))
                    {
                        // Parameter range is the same as the child binding
                        // If the parameter is non-zero, then the modifier binding is activated
                        if (InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "modifier",
                                out InputBinding modifier)
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "binding",
                                out InputBinding childBinding))
                        {
                            bool isModifierActive = !IsZeroInputParam(param);
                            foreach (var inp in GetInputsForBinding(modifier, isModifierActive))
                            {
                                yield return inp;
                            }
                            foreach (var inp in GetInputsForBinding(childBinding, param))
                            {
                                yield return inp;
                            }
                        }
                        else
                        {
                            RGDebug.LogWarning("Failed to find some bindings for OneModifierComposite");
                        }
                    } else if (typeof(TwoModifiersComposite).IsAssignableFrom(compositeType))
                    {
                        if (InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "modifier1",
                                out InputBinding modifier1)
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "modifier2", 
                                out InputBinding modifier2)
                            && InputActionAction.TryFindCompositePartBinding(inputAction, bindingIndex.Value, "binding",
                                out InputBinding childBinding))
                        {
                            bool isModifierActive = !IsZeroInputParam(param);
                            foreach (var inp in GetInputsForBinding(modifier1, isModifierActive))
                            {
                                yield return inp;
                            }
                            foreach (var inp in GetInputsForBinding(modifier2, isModifierActive))
                            {
                                yield return inp;
                            }
                            foreach (var inp in GetInputsForBinding(childBinding, param))
                            {
                                yield return inp;
                            }
                        }
                        else
                        {
                            RGDebug.LogWarning("Failed to find some bindings for TwoModifiersComposite");
                        }
                    }
                }
                else
                {
                    foreach (var inp in GetInputsForBinding(binding, param))
                    {
                        yield return inp;
                    }
                }
            }
        }
    }
}