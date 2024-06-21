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
    /// <summary>
    /// Action to click on a Unity UI button identified by the full name of the event listener method.
    /// </summary>
    public class UIButtonPressAction : RGGameAction
    {
        public string EventListenerName { get; }
        
        public UIButtonPressAction(string path, Type objectType, string eventListenerName, int actionGroup) : 
            base(path, objectType, actionGroup)
        {
            Debug.Assert(typeof(Button).IsAssignableFrom(objectType));
            EventListenerName = eventListenerName;
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();
        
        public override bool IsValidForObject(Object obj)
        {
            Button btn = (Button)obj;
            foreach (string listenerName in RGActionManagerUtils.GetEventListenerMethodNames(btn.onClick))
            {
                if (listenerName == EventListenerName)
                {
                    return true;
                }
            }
            return false;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new UIButtonPressInstance(this, obj);
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is UIButtonPressAction action)
            {
                return EventListenerName == action.EventListenerName;
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