﻿#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using RegressionGames.RGLegacyInputUtility;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action to bring an input axis into either a negative, zero, or positive state. (Input.GetAxis, Input.GetAxisRaw)
    /// This reads the configuration from the input manager settings and simulates the appropriate
    /// keyboard or mouse event associated with the axis configuration.
    /// </summary>
    public class LegacyAxisAction : RGGameAction
    {
        public Func<Object, string> AxisNameFunc { get; }
        public string AxisNameFuncName { get; }

        public float MouseMovementMagnitude = 10.0f; // for axes that read mouse delta, the amount to move/scroll the mouse

        public LegacyAxisAction(string[] path, Type objectType, Func<Object, string> axisNameFunc, string axisNameFuncName, int actionGroup) : 
            base(path, objectType, actionGroup)
        {
            AxisNameFunc = axisNameFunc;
            AxisNameFuncName = axisNameFuncName;
        }

        // Discretize the axis into three states (negative, zero, positive) so there is an equal chance of not going in either direction
        public override IRGValueRange ParameterRange { get; } = new RGIntRange(-1, 1);
        
        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new LegacyAxisInstance(this, obj);
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is LegacyAxisAction action)
            {
                return AxisNameFuncName == action.AxisNameFuncName;
            }
            return false;
        }
    }

    public class LegacyAxisInstance : RGGameActionInstance<LegacyAxisAction, int>
    {
        public LegacyAxisInstance(LegacyAxisAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override void PerformAction(int param)
        {
            string axisName = Action.AxisNameFunc(TargetObject);
            var inpSettings = RGLegacyInputWrapper.InputManagerSettings;
            
            // Simulate the appropriate input event for each entry associated with the axis name
            foreach (var entry in inpSettings.GetEntriesByName(axisName))
            {
                int paramForEntry = param;
                if (entry.invert)
                {
                    paramForEntry *= -1;
                }
                switch (entry.type)
                {
                    case InputManagerEntryType.KEY_OR_MOUSE_BUTTON:
                    {
                        if (paramForEntry == 1)
                        {
                            if (entry.positiveButtonKeyCode.HasValue)
                            {
                                RGActionManager.SimulateKeyState(entry.positiveButtonKeyCode.Value, true);
                            }
                            if (entry.altPositiveButtonKeyCode.HasValue)
                            {
                                RGActionManager.SimulateKeyState(entry.altPositiveButtonKeyCode.Value, true);
                            }
                            if (entry.negativeButtonKeyCode.HasValue)
                            {
                                RGActionManager.SimulateKeyState(entry.negativeButtonKeyCode.Value, false);
                            }
                            if (entry.altNegativeButtonKeyCode.HasValue)
                            {
                                RGActionManager.SimulateKeyState(entry.altNegativeButtonKeyCode.Value, false);
                            }
                        } else if (paramForEntry == -1)
                        {
                            if (entry.positiveButtonKeyCode.HasValue)
                            {
                                RGActionManager.SimulateKeyState(entry.positiveButtonKeyCode.Value, false);
                            }
                            if (entry.altPositiveButtonKeyCode.HasValue)
                            {
                                RGActionManager.SimulateKeyState(entry.altPositiveButtonKeyCode.Value, false);
                            }
                            if (entry.negativeButtonKeyCode.HasValue)
                            {
                                RGActionManager.SimulateKeyState(entry.negativeButtonKeyCode.Value, true);
                            }
                            if (entry.altNegativeButtonKeyCode.HasValue)
                            {
                                RGActionManager.SimulateKeyState(entry.altNegativeButtonKeyCode.Value, true);
                            }
                        }
                        else if (paramForEntry == 0)
                        {
                            if (entry.positiveButtonKeyCode.HasValue)
                            {
                                RGActionManager.SimulateKeyState(entry.positiveButtonKeyCode.Value, false);
                            }
                            if (entry.altPositiveButtonKeyCode.HasValue)
                            {
                                RGActionManager.SimulateKeyState(entry.altPositiveButtonKeyCode.Value, false);
                            }
                            if (entry.negativeButtonKeyCode.HasValue)
                            {
                                RGActionManager.SimulateKeyState(entry.negativeButtonKeyCode.Value, false);
                            }
                            if (entry.altNegativeButtonKeyCode.HasValue)
                            {
                                RGActionManager.SimulateKeyState(entry.altNegativeButtonKeyCode.Value, false);
                            }
                        }
                        else
                        {
                            RGDebug.LogWarning($"Unexpected parameter {param}");
                        }
                        break;
                    }
                    case InputManagerEntryType.MOUSE_MOVEMENT:
                    {
                        if (entry.sensitivity > 0.0f)
                        {
                            float mouseMoveScale = Action.MouseMovementMagnitude / entry.sensitivity;
                            if (entry.axis == 0) // X Axis
                            {
                                RGActionManager.SimulateMouseMovementDelta(Vector2.right * (paramForEntry * mouseMoveScale));
                            } else if (entry.axis == 1) // Y Axis
                            {
                                RGActionManager.SimulateMouseMovementDelta(Vector2.up * (paramForEntry * mouseMoveScale));
                            } else if (entry.axis == 2) // Scroll Wheel
                            {
                                RGActionManager.SimulateMouseScroll(Vector2.up * (paramForEntry * mouseMoveScale));
                            }
                        }
                        break;
                    }
                    case InputManagerEntryType.JOYSTICK_AXIS:
                        // joysticks currently unsupported
                        break;
                }
            }
        }
    }
}
#endif