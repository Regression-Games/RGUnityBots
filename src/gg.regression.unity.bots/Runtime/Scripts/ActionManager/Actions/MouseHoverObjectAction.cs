using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// This action is used to move the mouse to the center of an object's screen space bounding box (if true),
    /// or move the mouse outside the bounds of an object (if false).
    /// This is used to trigger events such as OnMouseOver(), OnMouseEnter(), and OnMouseExit().
    /// </summary>
    public class MouseHoverObjectAction : RGGameAction
    {
        public MouseHoverObjectAction(string[] path, Type objectType) : base(path, objectType)
        {
        }

        public MouseHoverObjectAction(JObject serializedAction) :
            base(serializedAction)
        {
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();

        public override string DisplayName => $"Mouse Hover Over {ObjectType.Name}";

        public override bool IsValidForObject(Object obj)
        {
            return RGActionManagerUtils.GetGameObjectScreenSpaceBounds(((Component)obj).gameObject).HasValue;
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



        protected override IEnumerable<RGActionInput> GetActionInputs(bool param)
        {
            var ssBounds = RGActionManagerUtils.GetGameObjectScreenSpaceBounds(((Component)TargetObject).gameObject);
            if (ssBounds.HasValue)
            {
                if (param)
                {
                    yield return new MousePositionInput(ssBounds.Value.center);
                }
                else
                {
                    yield return new MousePositionInput(RGActionManagerUtils.GetPointOutsideBounds(ssBounds.Value));
                }
            }
        }
    }
}