using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action to trigger a UI toggle.
    /// The action takes a bool parameter:
    /// if true it presses down the left mouse button on either the checkbox or label (depending on which is interactable/obscured)
    /// if false it releases the left mouse button over either the checkbox or label (depending on which is interactable/obscured)
    /// </summary>
    public class UITogglePressAction : RGGameAction
    {
        /// <summary>
        /// The normalized game object name is the name of the game object without a "(N)" suffix.
        /// This action will target UI toggles that have the given name.
        /// </summary>
        public string NormalizedGameObjectName { get; }

        /// <summary>
        /// If this toggle is part of a dropdown, then this is the name of the dropdown.
        /// </summary>
        public string NormalizedDropdownName { get; }

        public UITogglePressAction(string[] path, Type objectType, string normalizedGameObjectName, string normalizedDropdownName = null) : 
            base(path, objectType, new RGBoolRange())
        {
            Debug.Assert(typeof(Toggle).IsAssignableFrom(objectType));
            NormalizedGameObjectName = normalizedGameObjectName;
            NormalizedDropdownName = normalizedDropdownName;
        }

        public UITogglePressAction(JObject serializedAction) : base(serializedAction)
        {
            NormalizedGameObjectName = serializedAction["normalizedGameObjectName"].ToString();

            var dropdownName = serializedAction["normalizedDropdownName"];
            if (dropdownName.Type != JTokenType.Null)
            {
                NormalizedDropdownName = dropdownName.ToString();
            }
        }

        public override string DisplayName => $"Press {NormalizedGameObjectName}" 
                                              + (NormalizedDropdownName != null ? " (Dropdown " + NormalizedDropdownName + ")" : "");

        public static IEnumerable<CanvasRenderer> FindToggleRenderables(Toggle toggle)
        {
            foreach (var canvasRenderer in toggle.gameObject.transform.GetComponentsInChildren<CanvasRenderer>())
            {
                yield return canvasRenderer;
            }
        }
        
        public override bool IsValidForObject(Object obj)
        {
            Toggle toggle = (Toggle)obj;
            if (!RGActionManagerUtils.IsUIObjectInteractable(toggle))
            {
                return false;
            }
            
            Selectable parentDropdown = toggle.gameObject.transform.GetComponentInParent<Dropdown>(true);
            if (parentDropdown == null)
                parentDropdown = toggle.gameObject.transform.GetComponentInParent<TMP_Dropdown>(true);
            if (parentDropdown != null)
            {
                string dropdownName = UIObjectPressAction.GetNormalizedGameObjectName(parentDropdown.gameObject.name);
                if (dropdownName != NormalizedDropdownName)
                {
                    return false;
                }
            }

            string normName = UIObjectPressAction.GetNormalizedGameObjectName(toggle.gameObject.name);
            if (parentDropdown != null)
            {
                if (!normName.StartsWith(NormalizedGameObjectName))
                {
                    return false;
                }
            } else if (normName != NormalizedGameObjectName)
            {
                return false;
            }

            bool haveMousePos = false;
            foreach (var renderer in FindToggleRenderables(toggle))
            {
                haveMousePos = RGActionManagerUtils.GetUIMouseHitPosition(renderer.gameObject, out _);
                if (haveMousePos)
                    break;
            }
            
            return haveMousePos;
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is UITogglePressAction action)
            {
                return NormalizedGameObjectName == action.NormalizedGameObjectName &&
                       NormalizedDropdownName == action.NormalizedDropdownName;
            }
            return false;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new UITogglePressInstance(this, obj);
        }

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append(",\n\"normalizedGameObjectName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, NormalizedGameObjectName);
            stringBuilder.Append(",\n\"normalizedDropdownName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, NormalizedDropdownName);
        }
        
        public override IEnumerable<(string, string)> GetDisplayActionAttributes()
        {
            foreach (var attr in base.GetDisplayActionAttributes())
                yield return attr;

            yield return ("Target Game Object", NormalizedGameObjectName);
        }
    }

    public class UITogglePressInstance : RGGameActionInstance<UITogglePressAction, bool>
    {
        public UITogglePressInstance(UITogglePressAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override bool IsValidActionParameter(bool param)
        {
            return true;
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(bool param)
        {
            Toggle toggle = (Toggle)TargetObject;
            Vector2? mousePos = null;
            foreach (var renderer in UITogglePressAction.FindToggleRenderables(toggle))
            {
                if (RGActionManagerUtils.GetUIMouseHitPosition(renderer.gameObject, out Vector2 rendererMousePos))
                {
                    mousePos = rendererMousePos;
                    break;
                }
            }

            if (param)
            {
                if (mousePos.HasValue)
                {
                    yield return new MousePositionInput(mousePos.Value);
                    yield return new MouseButtonInput(MouseButtonInput.LEFT_MOUSE_BUTTON, true);
                }
            }
            else
            {
                if (mousePos.HasValue)
                {
                    yield return new MousePositionInput(mousePos.Value);
                }
                yield return new MouseButtonInput(MouseButtonInput.LEFT_MOUSE_BUTTON, false);
            }
        }
    }
}