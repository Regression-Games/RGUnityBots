using System;
using System.Collections.Generic;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
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
        
        public UIButtonPressAction(string[] path, Type objectType, string eventListenerName) : 
            base(path, objectType)
        {
            Debug.Assert(typeof(Button).IsAssignableFrom(objectType));
            EventListenerName = eventListenerName;
        }

        public UIButtonPressAction(RGSerializedAction serializedAction) :
            base(serializedAction)
        {
            EventListenerName = serializedAction.actionStringData;
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();
        
        public override bool IsValidForObject(Object obj)
        {
            Button btn = (Button)obj;
            if (!btn.IsInteractable())
            {
                return false;
            }

            Transform t = btn.transform.parent;
            while (t != null)
            {
                CanvasGroup canvasGroup = t.gameObject.GetComponent<CanvasGroup>();
                if (canvasGroup != null && (!canvasGroup.interactable || !canvasGroup.blocksRaycasts))
                {
                    return false;
                }
                t = t.parent;
            }
            
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

        protected override void Serialize(RGSerializedAction serializedAction)
        {
            serializedAction.actionStringData = EventListenerName;
        }
    }

    public class UIButtonPressInstance : RGGameActionInstance<UIButtonPressAction, bool>
    {
        public UIButtonPressInstance(UIButtonPressAction action, Object targetObject) : base(action, targetObject)
        {
        }

        private static Bounds? GetUIScreenSpaceBounds(Object targetObject)
        {
            Button targetBtn = (Button)targetObject;
            var instId = targetBtn.transform.GetInstanceID();
            if (RGActionManager.CurrentUITransforms.TryGetValue(instId, out TransformStatus tStatus))
            {
                return tStatus.screenSpaceBounds;
            }
            else
            {
                return null;
            }
        }
        
        protected override IEnumerable<RGActionInput> GetActionInputs(bool param)
        {
            if (param)
            {
                Bounds? bounds = GetUIScreenSpaceBounds(TargetObject);
                if (bounds.HasValue)
                {
                    Bounds boundsVal = bounds.Value;
                    yield return new MousePositionInput(boundsVal.center);
                    yield return new MouseButtonInput(MouseButtonId.LEFT_MOUSE_BUTTON, true);
                }
            }
            else
            {
                yield return new MouseButtonInput(MouseButtonId.LEFT_MOUSE_BUTTON, false);
            }
        }
    }
}