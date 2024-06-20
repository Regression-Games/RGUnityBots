
using System;
using RegressionGames.RGLegacyInputUtility;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    public class LegacyKeyReleaseIfPressed : RGGameAction
    {
        public Func<Object, KeyCode> KeyCodeFunc { get; }
        
        public LegacyKeyReleaseIfPressed(string path, Type objectType, Func<Object, KeyCode> keyCodeFunc) : base(path, objectType)
        {
            KeyCodeFunc = keyCodeFunc;
        }

        public override IRGValueRange ParameterRange => new RGVoidRange();

        public override bool IsValidForObject(Object obj)
        {
            KeyCode keyCode = KeyCodeFunc(obj);
            return RGLegacyInputWrapper.GetKey(keyCode);
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new LegacyKeyReleaseIfPressedInstance(this, obj);
        }
    }

    public class LegacyKeyReleaseIfPressedInstance : RGGameActionInstance<LegacyKeyReleaseIfPressed, object>
    {
        public LegacyKeyReleaseIfPressedInstance(LegacyKeyReleaseIfPressed action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override void PerformAction(object param)
        {
            KeyCode keyCode = Action.KeyCodeFunc(TargetObject);
            RGActionUtils.SimulateLegacyKeyState(keyCode, false);
        }
    }
}