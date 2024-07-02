using System;
using System.Collections.Generic;
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
        public MousePressObjectAction(string[] path, Type objectType) : base(path, objectType)
        {
        }

        public MousePressObjectAction(RGSerializedAction serializedAction) :
            base(serializedAction)
        {
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();

        public override string DisplayName => $"Mouse Press On {ObjectType.Name}";

        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new MousePressObjectInstance(this, obj);
        }

        protected override void Serialize(RGSerializedAction serializedAction)
        {
        }
        
        public override bool IsEquivalentTo(RGGameAction other)
        {
            return other is MousePressObjectAction && base.IsEquivalentTo(other);
        }
    }

    public class MousePressObjectInstance : RGGameActionInstance<MousePressObjectAction, bool>
    {
        public MousePressObjectInstance(MousePressObjectAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(bool param)
        {
            if (param)
            {
                var ssBounds = MouseHoverObjectInstance.GetHoverScreenSpaceBounds(TargetObject);
                if (ssBounds.HasValue)
                {
                    yield return new MousePositionInput(ssBounds.Value.center);
                    yield return new MouseButtonInput(MouseButtonInput.LEFT_MOUSE_BUTTON, true);
                }
            }
            else
            {
                yield return new MouseButtonInput(MouseButtonInput.LEFT_MOUSE_BUTTON, false);
            }
        }
    }
}