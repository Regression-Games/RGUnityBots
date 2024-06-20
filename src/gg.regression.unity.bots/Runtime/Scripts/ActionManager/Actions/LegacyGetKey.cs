#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions.LegacyInput
{
    public class LegacyGetKey : RGGameAction
    {
        public Func<KeyCode> KeyCodeFunc { get; private set; }
        
        public LegacyGetKey(string path, Type objectType, Func<KeyCode> keyCodeFunc) : base(path, objectType)
        {
            KeyCodeFunc = keyCodeFunc;
        }

        public override IRGValueRange ParameterRange => new RGBoolRange(false, true);
        
        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new LegacyGetKeyInstance(this, obj);
        }
    }

    public class LegacyGetKeyInstance : RGGameActionInstance<LegacyGetKey, bool>
    {
        public LegacyGetKeyInstance(LegacyGetKey action, Object instance) : base(action, instance)
        {
        }
        
        public override void Perform(bool param)
        {
            KeyCode keyCode = Action.KeyCodeFunc();
            RGActionUtils.SimulateLegacyKeyState(keyCode, param);
        }
    }
}
#endif