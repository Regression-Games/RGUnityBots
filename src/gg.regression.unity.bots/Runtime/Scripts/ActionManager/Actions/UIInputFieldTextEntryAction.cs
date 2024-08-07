using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action for entering and deleting text in a focused input field.
    /// This generates the following types of events, depending on the action parameter:
    /// 1. Press a key (and possibly the Shift key as well) and release all others 
    /// 2. Press backspace and release all other keys
    /// 3. Release all keys
    /// </summary>
    public class UIInputFieldTextEntryAction : RGGameAction
    {
        // Special parameter values
        public const int PARAM_NULL = 0; // no key entry (just release all keys)
        public const int PARAM_BACKSPACE = 1; // press backspace and release other keys
        public const int PARAM_FIRST_KEY = 2; // the first parameter index from which key entry starts
        
        public const Key MinKey = Key.Space;
        public const Key MaxKey = Key.Digit0;

        [RGActionProperty("Target Game Object", false)]
        public string NormalizedGameObjectName { get; set; }

        public UIInputFieldTextEntryAction(string[] path, Type objectType, string normalizedGameObjectName) : 
            base(path, objectType, 
                // Parameter 0 releases all keys (PARAM_NULL)
                // Parameter 1 presses backspace and releases all others (PARAM_BACKSPACE)
                // All even-numbered values afterwards are the key without any modifiers
                // All odd-numbered values afterwards are the key while holding shift (e.g. upper-case, symbols)
                new RGIntRange(0, 2 + (MaxKey - MinKey + 1)*2))
        {
            if (!(typeof(InputField).IsAssignableFrom(objectType) ||
                  typeof(TMP_InputField).IsAssignableFrom(objectType)))
            {
                throw new ArgumentException($"Expected input field, received {objectType}");
            }
            NormalizedGameObjectName = normalizedGameObjectName;
        }

        public UIInputFieldTextEntryAction(JObject serializedAction) : base(serializedAction)
        {
            NormalizedGameObjectName = serializedAction["normalizedGameObjectName"].ToString();
        }

        public override string DisplayName => $"Text Entry {NormalizedGameObjectName}";
        
        public override bool IsValidForObject(Object obj)
        {
            Selectable inputField = (Selectable)obj;
            if (!RGActionManagerUtils.IsUIObjectInteractable(inputField))
            {
                return false;
            }
            
            string normName = UIObjectPressAction.GetNormalizedGameObjectName(inputField.gameObject.name);
            if (normName != NormalizedGameObjectName)
            {
                return false;
            }

            if (inputField is InputField legacyInputField)
            {
                if (!legacyInputField.isFocused)
                {
                    return false;
                }
            } else if (inputField is TMP_InputField tmpInputField)
            {
                if (!tmpInputField.isFocused)
                {
                    return false;
                }
            }
            else
            {
                throw new Exception("Unexpected object " + obj);
            }

            return true;
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is UIInputFieldTextEntryAction action)
            {
                return NormalizedGameObjectName == action.NormalizedGameObjectName;
            }
            return false;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new UIInputFieldTextEntryInstance(this, obj);
        }

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append(",\n\"normalizedGameObjectName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, NormalizedGameObjectName);
        }
    }

    public class UIInputFieldTextEntryInstance : RGGameActionInstance<UIInputFieldTextEntryAction, int>
    {
        public UIInputFieldTextEntryInstance(UIInputFieldTextEntryAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override bool IsValidActionParameter(int param)
        {
            return true;
        }

        private IEnumerable<Key> TextEntryKeys()
        {
            for (Key key = UIInputFieldTextEntryAction.MinKey; key <= UIInputFieldTextEntryAction.MaxKey; ++key)
            {
                yield return key;
            }
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(int param)
        {
            if (param == UIInputFieldTextEntryAction.PARAM_NULL)
            {
                foreach (Key key in TextEntryKeys())
                {
                    yield return new InputSystemKeyInput(key, false);
                }
                yield return new InputSystemKeyInput(Key.Backspace, false);
                yield return new InputSystemKeyInput(Key.Enter, false);
                yield return new InputSystemKeyInput(Key.NumpadEnter, false);
                yield return new InputSystemKeyInput(Key.LeftShift, false);
                yield return new InputSystemKeyInput(Key.RightShift, false);
            } else if (param == UIInputFieldTextEntryAction.PARAM_BACKSPACE)
            {
                foreach (Key key in TextEntryKeys())
                {
                    if (key != Key.Backspace)
                    {
                        yield return new InputSystemKeyInput(key, false);
                    }
                }
                
                yield return new InputSystemKeyInput(Key.Enter, false);
                yield return new InputSystemKeyInput(Key.NumpadEnter, false);
                yield return new InputSystemKeyInput(Key.LeftShift, false);
                yield return new InputSystemKeyInput(Key.RightShift, false);
                yield return new InputSystemKeyInput(Key.Backspace, true);
            }
            else if (param >= UIInputFieldTextEntryAction.PARAM_FIRST_KEY)
            {
                int keyIndex = (param - 2) / 2;
                bool shiftPressed = param % 2 == 1;
                Key targetKey = UIInputFieldTextEntryAction.MinKey + keyIndex;
                if (shiftPressed)
                {
                    yield return new InputSystemKeyInput(Key.LeftShift, true);
                }
                else
                {
                    yield return new InputSystemKeyInput(Key.LeftShift, false);
                    yield return new InputSystemKeyInput(Key.RightShift, false);
                }
                
                yield return new InputSystemKeyInput(Key.Backspace, false);
                yield return new InputSystemKeyInput(Key.Enter, false);
                yield return new InputSystemKeyInput(Key.NumpadEnter, false);
                
                foreach (Key key in TextEntryKeys())
                {
                    if (key != targetKey && key != Key.LeftShift && key != Key.RightShift)
                    {
                        yield return new InputSystemKeyInput(key, false);
                    }
                }
                yield return new InputSystemKeyInput(targetKey, true);
            }
        }
    }
}
