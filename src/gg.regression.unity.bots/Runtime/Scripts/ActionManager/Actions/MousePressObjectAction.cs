using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
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
        public MousePressObjectAction(string[] path, Type objectType) : 
            base(path, objectType, new RGBoolRange())
        {
        }

        public MousePressObjectAction(JObject serializedAction) :
            base(serializedAction)
        {
        }

        public override string DisplayName => $"Mouse Press On {ObjectType.Name}";

        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new MousePressObjectInstance(this, obj);
        }
        
        public override bool IsEquivalentTo(RGGameAction other)
        {
            return other is MousePressObjectAction && base.IsEquivalentTo(other);
        }

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
        }
    }

    public class MousePressObjectInstance : RGGameActionInstance<MousePressObjectAction, bool>
    {
        public MousePressObjectInstance(MousePressObjectAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override bool IsValidActionParameter(bool param)
        {
            if (param)
            {
                // if trying to click on the object, it must be reachable by a raycast
                var gameObject = ((Component)TargetObject).gameObject;
                return RGActionManagerUtils.GetGameObjectMouseHitPosition(gameObject, out _);
            }
            else
            {
                // if trying to release the object, assume this is always possible (at minimum by just releasing the mouse button)
                return true;
            }
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(bool param)
        {
            var gameObject = ((Component)TargetObject).gameObject;
            if (param)
            {
                if (RGActionManagerUtils.GetGameObjectMouseHitPosition(gameObject, out Vector2 mousePos))
                {
                    yield return new MousePositionInput(mousePos);
                    yield return new MouseButtonInput(MouseButtonInput.LEFT_MOUSE_BUTTON, true);
                }
            }
            else
            {
                if (RGActionManagerUtils.GetGameObjectMouseHitPosition(gameObject, out Vector2 mousePos))
                {
                    yield return new MousePositionInput(mousePos);
                }
                yield return new MouseButtonInput(MouseButtonInput.LEFT_MOUSE_BUTTON, false);
            }
        }
    }
}