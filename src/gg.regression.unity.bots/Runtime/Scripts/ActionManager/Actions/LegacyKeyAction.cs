#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action for pressing or releasing a key identified by a legacy key code identifier.
    /// </summary>
    public class LegacyKeyAction : RGGameAction
    {
        public Func<Object, KeyCode> KeyCodeFunc { get; }
        public string KeyCodeFuncName { get; }
        
        public LegacyKeyAction(string path, Type objectType, Func<Object, KeyCode> keyCodeFunc, string keyCodeFuncName) : base(path, objectType)
        {
            KeyCodeFunc = keyCodeFunc;
            KeyCodeFuncName = keyCodeFuncName;
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();

        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new LegacyKeyInstance(this, obj);
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is LegacyKeyAction action)
            {
                return KeyCodeFuncName == action.KeyCodeFuncName;
            }
            return false;
        }
    }

    public class LegacyKeyInstance : RGGameActionInstance<LegacyKeyAction, bool>
    {
        public LegacyKeyInstance(LegacyKeyAction action, Object targetObject) : base(action, targetObject)
        {
        }
        
        protected override void PerformAction(bool param)
        {
            KeyCode keyCode = Action.KeyCodeFunc(TargetObject);
            RGActionManager.SimulateLegacyKeyState(keyCode, param);
        }
    }
}
#endif