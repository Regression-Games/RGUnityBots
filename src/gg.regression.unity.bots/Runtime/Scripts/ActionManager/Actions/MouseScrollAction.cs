using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action for simulating a mouse scroll.
    /// </summary>
    public class MouseScrollAction : RGGameAction
    {
        public MouseScrollAction(string[] path, Type objectType) : 
            base(path, objectType)
        {
        }

        public MouseScrollAction(JObject serializedAction) :
            base(serializedAction)
        {
        }

        // Discretized to int (rather than using float) so that there is a greater chance of not scrolling at all
        public override IRGValueRange ParameterRange { get; } =
            new RGVector2IntRange(new Vector2Int(-1, -1), new Vector2Int(1, 1));

        public override string DisplayName => "Mouse Scroll";

        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new MouseScrollInstance(this, obj);
        }
        
        public override bool IsEquivalentTo(RGGameAction other)
        {
            return other is MouseScrollAction && base.IsEquivalentTo(other);
        }

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
        }
    }

    public class MouseScrollInstance : RGGameActionInstance<MouseScrollAction, Vector2Int>
    {
        public MouseScrollInstance(MouseScrollAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(Vector2Int param)
        {
            Vector2 mouseScroll = param;
            yield return new MouseScrollInput(mouseScroll);
        }
    }
}