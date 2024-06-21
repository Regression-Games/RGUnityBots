
using System;
using RegressionGames.RGLegacyInputUtility;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action to press a key identified by a legacy key code that is valid
    /// only if the key is already released.
    /// </summary>
    public class LegacyKeyPressIfReleased : RGGameAction
    {
        public Func<Object, KeyCode> KeyCodeFunc { get; }
        
        public LegacyKeyPressIfReleased(string path, Type objectType, Func<Object, KeyCode> keyCodeFunc) : base(path, objectType)
        {
            KeyCodeFunc = keyCodeFunc;
        }

        public override IRGValueRange ParameterRange { get; } = new RGVoidRange();

        public override bool IsValidForObject(Object obj)
        {
            KeyCode keyCode = KeyCodeFunc(obj);
            return !RGLegacyInputWrapper.GetKey(keyCode);
        }
        
        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new LegacyKeyPressIfReleasedInstance(this, obj);
        }
    }

    public class LegacyKeyPressIfReleasedInstance : RGGameActionInstance<LegacyKeyPressIfReleased, object>
    {
        public LegacyKeyPressIfReleasedInstance(LegacyKeyPressIfReleased action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override void PerformAction(object param)
        {
            KeyCode keyCode = Action.KeyCodeFunc(TargetObject);
            RGActionManager.SimulateLegacyKeyState(keyCode, true);
        }
    }
}