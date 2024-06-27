using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    public enum MouseButtonActionButton
    {
        LEFT_MOUSE_BUTTON,
        MIDDLE_MOUSE_BUTTON,
        RIGHT_MOUSE_BUTTON,
        FORWARD_MOUSE_BUTTON,
        BACK_MOUSE_BUTTON
    }
    
    /// <summary>
    /// This is an action to press or release a particular mouse button.
    /// </summary>
    public class MouseButtonAction : RGGameAction
    {
        public RGActionParamFunc<MouseButtonActionButton> MouseButtonFunc { get; }
        
        public MouseButtonAction(string[] path, Type objectType, RGActionParamFunc<MouseButtonActionButton> mouseButtonFunc, int actionGroup) 
            : base(path, objectType, actionGroup)
        {
            MouseButtonFunc = mouseButtonFunc;
        }

        public MouseButtonAction(RGSerializedAction serializedAction) :
            base(serializedAction)
        {
            MouseButtonFunc = new RGActionParamFunc<MouseButtonActionButton>((string)serializedAction.actionParameters[0]);
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();
        
        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new MouseButtonInstance(this, obj);
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is MouseButtonAction action)
            {
                return action.MouseButtonFunc == MouseButtonFunc;
            }
            else
            {
                return false;
            }
        }

        protected override void SerializeParameters(List<object> actionParametersOut)
        {
            actionParametersOut.Add(MouseButtonFunc.Identifier);
        }
    }

    public class MouseButtonInstance : RGGameActionInstance<MouseButtonAction, bool>
    {
        public MouseButtonInstance(MouseButtonAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override void PerformAction(bool param)
        {
            MouseButtonActionButton btn = Action.MouseButtonFunc.Invoke(TargetObject);
            switch (btn)
            {
                case MouseButtonActionButton.LEFT_MOUSE_BUTTON:
                    RGActionManager.SimulateLeftMouseButton(param);
                    break;
                case MouseButtonActionButton.MIDDLE_MOUSE_BUTTON:
                    RGActionManager.SimulateMiddleMouseButton(param);
                    break;
                case MouseButtonActionButton.RIGHT_MOUSE_BUTTON:
                    RGActionManager.SimulateRightMouseButton(param);
                    break;
                case MouseButtonActionButton.FORWARD_MOUSE_BUTTON:
                    RGActionManager.SimulateForwardMouseButton(param);
                    break;
                case MouseButtonActionButton.BACK_MOUSE_BUTTON:
                    RGActionManager.SimulateBackMouseButton(param);
                    break;
                default:
                    RGDebug.LogWarning($"Unexpected mouse button {btn}");
                    break;
            }
        }
    }
}