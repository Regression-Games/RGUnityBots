#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action for pressing or releasing a key identified by a legacy key code identifier.
    /// </summary>
    public class LegacyKeyPressOrRelease : RGGameAction
    {
        public Func<Object, KeyCode> KeyCodeFunc { get; private set; }
        
        public LegacyKeyPressOrRelease(string path, Type objectType, Func<Object, KeyCode> keyCodeFunc) : base(path, objectType)
        {
            KeyCodeFunc = keyCodeFunc;
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange(false, true);

        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new LegacyKeyPressOrReleaseInstance(this, obj);
        }
    }

    public class LegacyKeyPressOrReleaseInstance : RGGameActionInstance<LegacyKeyPressOrRelease, bool>
    {
        public LegacyKeyPressOrReleaseInstance(LegacyKeyPressOrRelease action, Object targetObject) : base(action, targetObject)
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