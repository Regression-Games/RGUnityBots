#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using System.Collections.Generic;
using RegressionGames.RGLegacyInputUtility;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action to either press or release a button checked via the Input.GetButton API.
    /// This reads the input manager settings to determine the appropriate key codes to simulate.
    /// </summary>
    public class LegacyButtonAction : RGGameAction
    {
        public RGActionParamFunc<string> ButtonNameFunc { get; }

        public LegacyButtonAction(string[] path, Type objectType, RGActionParamFunc<string> buttonNameFunc) : 
            base(path, objectType)
        {
            ButtonNameFunc = buttonNameFunc;
        }

        public LegacyButtonAction(RGSerializedAction serializedAction) :
            base(serializedAction)
        {
            ButtonNameFunc = RGActionParamFunc<string>.Deserialize(serializedAction.actionFuncType, serializedAction.actionFuncData);
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();
        
        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new LegacyButtonInstance(this, obj);
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is LegacyButtonAction action)
            {
                return ButtonNameFunc == action.ButtonNameFunc;
            }
            return false;
        }

        protected override void Serialize(RGSerializedAction serializedAction)
        {
            (serializedAction.actionFuncType, serializedAction.actionFuncData) = ButtonNameFunc.Serialize();
        }
    }

    public class LegacyButtonInstance : RGGameActionInstance<LegacyButtonAction, bool>
    {
        public LegacyButtonInstance(LegacyButtonAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(bool param)
        {
            string buttonName = Action.ButtonNameFunc.Invoke(TargetObject);
            var inpSettings = RGLegacyInputWrapper.InputManagerSettings;
            
            // Simulate appropriate button events
            foreach (var entry in inpSettings.GetEntriesByName(buttonName))
            {
                // The Input Manager considers either a positive or negative key code to be sufficient to trigger the button
                if (entry.positiveButtonKeyCode.HasValue)
                {
                    yield return new LegacyKeyInput(entry.positiveButtonKeyCode.Value, param);
                }
                if (entry.altPositiveButtonKeyCode.HasValue)
                {
                    yield return new LegacyKeyInput(entry.altPositiveButtonKeyCode.Value, param);
                }
                if (entry.negativeButtonKeyCode.HasValue)
                {
                    yield return new LegacyKeyInput(entry.negativeButtonKeyCode.Value, param);
                }
                if (entry.altNegativeButtonKeyCode.HasValue)
                {
                    yield return new LegacyKeyInput(entry.altNegativeButtonKeyCode.Value, param);
                }
            }
        }
    }
}
#endif