using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// This action is used to move the mouse to the center of an object's screen space bounding box (if true),
    /// or move the mouse outside the bounds of an object (if false).
    /// This is used to trigger events such as OnMouseOver(), OnMouseEnter(), and OnMouseExit().
    /// </summary>
    public class MouseHoverObjectAction : RGGameAction
    {
        public MouseHoverObjectAction(string[] path, Type objectType) : 
            base(path, objectType, new RGBoolRange())
        {
        }

        public MouseHoverObjectAction(JObject serializedAction) :
            base(serializedAction)
        {
        }

        public override string DisplayName => $"Mouse Hover Over {ObjectType.Name}";

        public override bool IsValidForObject(Object obj)
        {
            var gameObject = ((Component)obj).gameObject;
            return RGActionManagerUtils.GetGameObjectScreenSpaceBounds(gameObject).HasValue;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new MouseHoverObjectInstance(this, obj);
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            return other is MouseHoverObjectAction && base.IsEquivalentTo(other);
        }

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
        }
    }

    public class MouseHoverObjectInstance : RGGameActionInstance<MouseHoverObjectAction, bool>
    {
        public MouseHoverObjectInstance(MouseHoverObjectAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override bool IsValidActionParameter(bool param)
        {
            if (param)
            {
                // if trying to hover over the object, it must be reachable by a raycast
                var gameObject = ((Component)TargetObject).gameObject;
                return RGActionManagerUtils.GetGameObjectMouseHitPosition(gameObject, out _);
            }
            else
            {
                // if trying to hover outside the object, only need the screen space bounds (already determined by IsValidForObject)
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
                }
            }
            else
            {
                var ssBounds = RGActionManagerUtils.GetGameObjectScreenSpaceBounds(gameObject);
                if (ssBounds.HasValue)
                {
                    yield return new MousePositionInput(RGActionManagerUtils.GetPointOutsideBounds(ssBounds.Value));
                }
            }
        }
    }
}
