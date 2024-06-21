using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action to move the mouse cursor over non-UI parts of the screen. This specifically targets
    /// mouse position read by game object components via Input.mousePosition or Mouse.current.position,
    /// so it avoids any parts of the screen being covered by a UI element.
    /// </summary>
    public class MousePositionAction : RGGameAction
    {
        public MousePositionAction(string path, Type objectType, int actionGroup) : 
            base(path, objectType, actionGroup)
        {
        }

        public override IRGValueRange ParameterRange { get; } = new RGVector2Range(Vector2.zero, Vector2.one);
        
        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new MousePositionInstance(this, obj);
        }
    }

    public class MousePositionInstance : RGGameActionInstance<MousePositionAction, Vector2>
    {
        public MousePositionInstance(MousePositionAction action, Object targetObject) : base(action, targetObject)
        {
        }

        private bool IsCoordOverUIElement(Vector2 pos)
        {
            foreach (var p in RGActionManager.CurrentUITransforms)
            {
                var tStatus = p.Value;
                if (tStatus.screenSpaceBounds.HasValue && tStatus.screenSpaceBounds.Value.Contains(pos))
                {
                    return true;
                }
            }
            return false;
        }

        protected override void PerformAction(Vector2 param)
        {
            Vector2 mousePos = new Vector2(Screen.width * param.x, Screen.height * param.y);
            if (!IsCoordOverUIElement(mousePos))
            {
                RGActionManager.SimulateMouseMovement(mousePos);
            }
        }
    }
}