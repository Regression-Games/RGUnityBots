#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using RegressionGames.RGLegacyInputUtility;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    public class LegacyButtonAction : RGGameAction
    {
        public Func<Object, string> ButtonNameFunc { get; }
        public string ButtonNameFuncName { get; }

        public LegacyButtonAction(string[] path, Type objectType, Func<Object, string> buttonNameFunc, string buttonNameFuncName, int actionGroup) : 
            base(path, objectType, actionGroup)
        {
            ButtonNameFunc = buttonNameFunc;
            ButtonNameFuncName = buttonNameFuncName;
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
                return ButtonNameFuncName == action.ButtonNameFuncName;
            }
            return false;
        }
    }

    public class LegacyButtonInstance : RGGameActionInstance<LegacyButtonAction, bool>
    {
        public LegacyButtonInstance(LegacyButtonAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override void PerformAction(bool param)
        {
            string buttonName = Action.ButtonNameFunc(TargetObject);
            var inpSettings = RGLegacyInputWrapper.InputManagerSettings;
            
            // Simulate appropriate button events
            foreach (var entry in inpSettings.GetEntriesByName(buttonName))
            {
                // The Input Manager considers either a positive or negative key code to be sufficient to trigger the button
                if (entry.positiveButtonKeyCode.HasValue)
                {
                    RGActionManager.SimulateKeyState(entry.positiveButtonKeyCode.Value, param);
                }
                if (entry.altPositiveButtonKeyCode.HasValue)
                {
                    RGActionManager.SimulateKeyState(entry.altPositiveButtonKeyCode.Value, param);
                }
                if (entry.negativeButtonKeyCode.HasValue)
                {
                    RGActionManager.SimulateKeyState(entry.negativeButtonKeyCode.Value, param);
                }
                if (entry.altNegativeButtonKeyCode.HasValue)
                {
                    RGActionManager.SimulateKeyState(entry.altNegativeButtonKeyCode.Value, param);
                }
            }
        }
    }
}
#endif