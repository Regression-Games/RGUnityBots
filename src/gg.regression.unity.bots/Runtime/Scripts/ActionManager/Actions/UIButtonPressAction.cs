using System;
using System.Linq;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    public class UIButtonPressAction : RGGameAction
    {
        public string ButtonPath { get; }
        
        public UIButtonPressAction(string path, Type objectType, string buttonPath) : base(path, objectType)
        {
            Debug.Assert(typeof(Button).IsAssignableFrom(objectType));
            ButtonPath = buttonPath;
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();
        
        public override bool IsValidForObject(Object obj)
        {
            Button btn = (Button)obj;
            GameObject gameObject = btn.gameObject;
            TransformStatus tStatus = TransformStatus.GetOrCreateTransformStatus(gameObject.transform);
            return ButtonPath == tStatus.NormalizedPath;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new UIButtonPressInstance(this, obj);
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is UIButtonPressAction action)
            {
                return ButtonPath == action.ButtonPath;
            }
            return false;
        }
    }

    public class UIButtonPressInstance : RGGameActionInstance<UIButtonPressAction, bool>
    {
        public UIButtonPressInstance(UIButtonPressAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override void PerformAction(bool param)
        {
            if (param)
            {
                Button targetBtn = (Button)TargetObject;
                var instId = targetBtn.transform.GetInstanceID();
                if (RGActionManager.CurrentUITransforms.TryGetValue(instId, out TransformStatus tStatus))
                {
                    Bounds? bounds = tStatus.screenSpaceBounds;
                    if (bounds.HasValue)
                    {
                        Bounds boundsVal = bounds.Value;
                        RGActionManager.SimulateMouseMovement(boundsVal.center);
                        RGActionManager.SimulateLeftMouseButton(true);
                    }
                }
            }
            else
            {
                RGActionManager.SimulateLeftMouseButton(false);
            }
        }
    }
}