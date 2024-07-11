using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
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

        private static List<RaycastResult> _raycastResultCache = new List<RaycastResult>();
        
        public UIButtonPressAction(string[] path, Type objectType, string eventListenerName) : 
            base(path, objectType)
        {
            Debug.Assert(typeof(Button).IsAssignableFrom(objectType));
            EventListenerName = eventListenerName;
        }

        public UIButtonPressAction(JObject serializedAction) :
            base(serializedAction)
        {
            EventListenerName = serializedAction["eventListenerName"].ToString();
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();

        public override string DisplayName => $"{EventListenerName}";

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

            bool matchesListener = false;
            foreach (string listenerName in RGActionManagerUtils.GetEventListenerMethodNames(btn.onClick))
            {
                if (listenerName == EventListenerName)
                {
                    matchesListener = true;
                    break;
                }
            }
            if (!matchesListener)
                return false;

            bool haveMousePos = RGActionManagerUtils.GetUIMouseHitPosition(btn.gameObject, out _);
            return haveMousePos;
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

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append(",\n\"eventListenerName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, EventListenerName);
        }
    }

    public class UIButtonPressInstance : RGGameActionInstance<UIButtonPressAction, bool>
    {
        public UIButtonPressInstance(UIButtonPressAction action, Object targetObject) : base(action, targetObject)
        {
        }

        
        protected override IEnumerable<RGActionInput> GetActionInputs(bool param)
        {
            Button targetBtn = (Button)TargetObject;
            bool haveMousePos = RGActionManagerUtils.GetUIMouseHitPosition(targetBtn.gameObject, out Vector2 mousePos);
            if (param)
            {
                if (haveMousePos)
                {
                    yield return new MousePositionInput(mousePos);
                    yield return new MouseButtonInput(MouseButtonInput.LEFT_MOUSE_BUTTON, true);
                }
            }
            else
            {
                if (haveMousePos)
                {
                    yield return new MousePositionInput(mousePos);
                }
                yield return new MouseButtonInput(MouseButtonInput.LEFT_MOUSE_BUTTON, false);
            }
        }
    }
}
