using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action to click on a Unity UI component.
    /// The action takes a bool parameter:
    /// if true it presses down the left mouse button on the component, if false it releases the left mouse button over the component.
    /// </summary>
    public class UIObjectPressAction : RGGameAction
    {
        /// <summary>
        /// The normalized game object name is the name of the game object without a "(N)" suffix.
        /// This action will target UI objects that have the given name.
        /// </summary>
        public string NormalizedGameObjectName { get; }

        public UIObjectPressAction(string[] path, Type objectType, string normalizedGameObjectName) : 
            base(path, objectType, new RGBoolRange())
        {
            Debug.Assert(typeof(Selectable).IsAssignableFrom(objectType));
            NormalizedGameObjectName = normalizedGameObjectName;
        }

        public UIObjectPressAction(JObject serializedAction) : base(serializedAction)
        {
            NormalizedGameObjectName = serializedAction["normalizedGameObjectName"].ToString();
        }

        public override string DisplayName => $"Press {NormalizedGameObjectName}";
        
        public override bool IsValidForObject(Object obj)
        {
            Selectable uiComponent = (Selectable)obj;
            
            if (!RGActionManagerUtils.IsUIObjectInteractable(uiComponent))
            {
                return false;
            }

            string normGameObjectName = GetNormalizedGameObjectName(uiComponent.gameObject.name);
            if (normGameObjectName != NormalizedGameObjectName)
            {
                return false;
            }
            
            bool haveMousePos = RGActionManagerUtils.GetUIMouseHitPosition(uiComponent.gameObject, out _);
            return haveMousePos;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new UIObjectPressInstance(this, obj);
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is UIObjectPressAction action)
            {
                return NormalizedGameObjectName == action.NormalizedGameObjectName;
            }
            return false;
        }

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append(",\n\"normalizedGameObjectName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, NormalizedGameObjectName);
        }

        public static string GetNormalizedGameObjectName(string gameObjectName)
        {
            int parenIndex = gameObjectName.IndexOf('(');
            if (parenIndex >= 0)
            {
                gameObjectName = gameObjectName.Substring(0, parenIndex);
            }
            gameObjectName = gameObjectName.Trim();
            return gameObjectName;
        }

        public override IEnumerable<(string, string)> GetDisplayActionAttributes()
        {
            foreach (var attr in base.GetDisplayActionAttributes())
                yield return attr;

            yield return ("Target Game Object", NormalizedGameObjectName);
        }
    }

    public class UIObjectPressInstance : RGGameActionInstance<UIObjectPressAction, bool>
    {
        public UIObjectPressInstance(UIObjectPressAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override bool IsValidActionParameter(bool param)
        {
            return true;
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(bool param)
        {
            Selectable uiComponent = (Selectable)TargetObject;
            bool haveMousePos =
                RGActionManagerUtils.GetUIMouseHitPosition(uiComponent.gameObject, out Vector2 mousePos);
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