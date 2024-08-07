using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
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
        [RGActionProperty("Target Event Listener", false)]
        public string EventListenerName { get; }

        
        public UIButtonPressAction(string[] path, Type objectType, string eventListenerName) : 
            base(path, objectType, new RGBoolRange())
        {
            if (!typeof(Button).IsAssignableFrom(objectType))
            {
                throw new ArgumentException($"Expected button, received {objectType}");
            }
            EventListenerName = eventListenerName;
        }

        public UIButtonPressAction(JObject serializedAction) :
            base(serializedAction)
        {
            EventListenerName = serializedAction["eventListenerName"].ToString();
        }

        public override string DisplayName => $"{EventListenerName}";

        public override bool IsValidForObject(Object obj)
        {
            Button btn = (Button)obj;

            if (!RGActionManagerUtils.IsUIObjectInteractable(btn))
            {
                return false;
            }

            // If this action does not target the event listeners associated with this button, then this action is not valid for this button
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

            // Finally, check that it is actually possible to click the button via a raycast (i.e. that it is not obscured by another UI element)
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


        protected override bool IsValidActionParameter(bool param)
        {
            return true;
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
