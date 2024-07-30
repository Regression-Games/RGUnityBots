using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action for pressing/releasing the submit key on the focused input field.
    /// </summary>
    public class UIInputFieldSubmitAction : RGGameAction
    {
        public string NormalizedGameObjectName { get; set; }
        
        public UIInputFieldSubmitAction(string[] path, Type objectType, string normalizedGameObjectName) : 
            base(path, objectType, new RGBoolRange())
        {
            Debug.Assert(typeof(InputField).IsAssignableFrom(objectType));
            NormalizedGameObjectName = normalizedGameObjectName;
        }

        public UIInputFieldSubmitAction(JObject serializedAction) : base(serializedAction)
        {
            NormalizedGameObjectName = serializedAction["normalizedGameObjectName"].ToString();
        }

        public override string DisplayName => $"Text Submit {NormalizedGameObjectName}";
        
        public override bool IsValidForObject(Object obj)
        {
            Selectable inputField = (Selectable)obj;
            
            string normName = UIObjectPressAction.GetNormalizedGameObjectName(inputField.gameObject.name);
            if (normName != NormalizedGameObjectName)
            {
                return false;
            }
            
            // At least one selectable of the input field must be focused to be able to submit text
            if (!inputField.transform.GetComponentsInChildren<Selectable>().Any(RGActionManagerUtils.IsUIObjectFocused))
            {
                return false;
            }

            return true;
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is UIInputFieldSubmitAction action)
            {
                return NormalizedGameObjectName == action.NormalizedGameObjectName;
            }
            return false;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new UIInputFieldSubmitInstance(this, obj);
        }

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append(",\n\"normalizedGameObjectName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, NormalizedGameObjectName);
        }
        
        public override IEnumerable<(string, string)> GetDisplayActionAttributes()
        {
            foreach (var attr in base.GetDisplayActionAttributes())
                yield return attr;

            yield return ("Target Game Object", NormalizedGameObjectName);
        }
    }

    public class UIInputFieldSubmitInstance : RGGameActionInstance<UIInputFieldSubmitAction, bool>
    {
        public UIInputFieldSubmitInstance(UIInputFieldSubmitAction action, Object targetObject) : 
            base(action, targetObject)
        {
        }

        protected override bool IsValidActionParameter(bool param)
        {
            return true;
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(bool param)
        {
            yield return new InputSystemKeyInput(Key.Enter, param);
        }
    }
}