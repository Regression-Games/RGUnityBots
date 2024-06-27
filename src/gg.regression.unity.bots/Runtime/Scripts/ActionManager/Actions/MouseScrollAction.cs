using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action for simulating a mouse scroll.
    /// </summary>
    public class MouseScrollAction : RGGameAction
    {
        public MouseScrollAction(string[] path, Type objectType, int actionGroup) : base(path, objectType, actionGroup)
        {
        }

        // Discretized to int (rather than using float) so that there is a greater chance of not scrolling at all
        public override IRGValueRange ParameterRange { get; } =
            new RGVector2IntRange(new Vector2Int(-1, -1), new Vector2Int(1, 1));
        
        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new MouseScrollInstance(this, obj);
        }
    }

    public class MouseScrollInstance : RGGameActionInstance<MouseScrollAction, Vector2Int>
    {
        public MouseScrollInstance(MouseScrollAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override void PerformAction(Vector2Int param)
        {
            Vector2 mouseScroll = param;
            RGActionManager.SimulateMouseScroll(mouseScroll);
        }
    }
}