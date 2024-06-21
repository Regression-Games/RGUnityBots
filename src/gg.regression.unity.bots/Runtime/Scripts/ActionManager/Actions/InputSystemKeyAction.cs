using System;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action to simulate a key press or release represented by an Input System key code.
    /// </summary>
    public class InputSystemKeyAction : RGGameAction
    {
        public Func<Object, Key> KeyFunc { get; }
        public string KeyFuncName { get; }
        
        public InputSystemKeyAction(string path, Type objectType, Func<Object, Key> keyFunc, string keyFuncName, int actionGroup) : 
            base(path, objectType, actionGroup)
        {
            KeyFunc = keyFunc;
            KeyFuncName = keyFuncName;
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();
        
        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new InputSystemKeyInstance(this, obj);
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is InputSystemKeyAction action)
            {
                return KeyFuncName == action.KeyFuncName;
            }
            else
            {
                return false;
            }
        }
    }

    public class InputSystemKeyInstance : RGGameActionInstance<InputSystemKeyAction, bool>
    {
        public InputSystemKeyInstance(InputSystemKeyAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override void PerformAction(bool param)
        {
            Key key = Action.KeyFunc(TargetObject);
            RGActionManager.SimulateKeyState(key, param);
        }
    }
}