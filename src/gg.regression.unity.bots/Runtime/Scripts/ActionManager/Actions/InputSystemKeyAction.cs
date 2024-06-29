using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action to simulate a key press or release represented by an Input System key code.
    /// </summary>
    public class InputSystemKeyAction : RGGameAction
    {
        public RGActionParamFunc<Key> KeyFunc { get; }
        
        public InputSystemKeyAction(string[] path, Type objectType, RGActionParamFunc<Key> keyFunc, int actionGroup) : 
            base(path, objectType, actionGroup)
        {
            KeyFunc = keyFunc;
        }

        public InputSystemKeyAction(RGSerializedAction serializedAction) :
            base(serializedAction)
        {
            KeyFunc = RGActionParamFunc<Key>.Deserialize(serializedAction.actionFuncType, serializedAction.actionFuncData);
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
                return KeyFunc == action.KeyFunc;
            }
            else
            {
                return false;
            }
        }

        protected override void Serialize(RGSerializedAction serializedAction)
        {
            (serializedAction.actionFuncType, serializedAction.actionFuncData) = KeyFunc.Serialize();
        }
    }

    public class InputSystemKeyInstance : RGGameActionInstance<InputSystemKeyAction, bool>
    {
        public InputSystemKeyInstance(InputSystemKeyAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override void PerformAction(bool param)
        {
            Key key = Action.KeyFunc.Invoke(TargetObject);
            RGActionManager.SimulateKeyState(key, param);
        }
    }
}