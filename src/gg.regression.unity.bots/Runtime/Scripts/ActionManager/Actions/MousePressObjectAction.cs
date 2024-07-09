using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// If the parameter is true, then this action moves the mouse cursor to the center of the object's bounding box
    /// and presses the left mouse button.
    /// If the parameter is false, releases the left mouse button.
    /// This is used to trigger OnMouseDown(), OnMouseUp(), OnMouseUpAsButton(), and OnMouseDrag()
    /// </summary>
    public class MousePressObjectAction : RGGameAction
    {
        public MousePressObjectAction(string[] path, Type objectType, int actionGroup) : base(path, objectType, actionGroup)
        {
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();
        
        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new MousePressObjectInstance(this, obj);
        }
    }

    public class MousePressObjectInstance : RGGameActionInstance<MousePressObjectAction, bool>
    {
        public MousePressObjectInstance(MousePressObjectAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override void PerformAction(bool param)
        {
            if (param)
            {
                var ssBounds = MouseHoverObjectInstance.GetHoverScreenSpaceBounds(TargetObject);
                if (ssBounds.HasValue)
                {
                    RGActionManager.SimulateMouseMovement(ssBounds.Value.center);
                    RGActionManager.SimulateLeftMouseButton(true);
                }
            }
            else
            {
                RGActionManager.SimulateLeftMouseButton(false);
            }
        }
    }
}