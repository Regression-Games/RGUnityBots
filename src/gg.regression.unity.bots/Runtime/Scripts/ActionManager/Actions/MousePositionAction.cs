﻿using System;
using System.Collections.Generic;
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
        public MousePositionAction(string[] path, Type objectType) : 
            base(path, objectType)
        {
        }

        public MousePositionAction(RGSerializedAction serializedAction) :
            base(serializedAction)
        {
        }

        public override IRGValueRange ParameterRange { get; } = new RGVector2Range(Vector2.zero, Vector2.one);

        public override string DisplayName => "Mouse Position";

        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new MousePositionInstance(this, obj);
        }

        protected override void Serialize(RGSerializedAction serializedAction)
        {
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

        protected override IEnumerable<RGActionInput> GetActionInputs(Vector2 param)
        {
            Vector2 mousePos = new Vector2(Screen.width * param.x, Screen.height * param.y);
            if (!IsCoordOverUIElement(mousePos))
            {
                yield return new MousePositionInput(mousePos);
            }
        }
    }
}