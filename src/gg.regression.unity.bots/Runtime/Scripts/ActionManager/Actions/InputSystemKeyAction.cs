using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action to simulate a key press or release represented by an Input System key code.
    /// </summary>
    public class InputSystemKeyAction : RGGameAction
    {
        [RGActionProperty("Key", true)]
        public RGActionParamFunc<Key> KeyFunc { get; }
        
        public InputSystemKeyAction(string[] path, Type objectType, RGActionParamFunc<Key> keyFunc) : 
            base(path, objectType, new RGBoolRange())
        {
            KeyFunc = keyFunc;
        }

        public InputSystemKeyAction(JObject serializedAction) :
            base(serializedAction)
        {
            KeyFunc = RGActionParamFunc<Key>.Deserialize(serializedAction["keyFunc"]);
        }

        public override string DisplayName => $"Key {KeyFunc}";

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

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append(",\n\"keyFunc\":");
            KeyFunc.WriteToStringBuilder(stringBuilder);
        }
    }

    public class InputSystemKeyInstance : RGGameActionInstance<InputSystemKeyAction, bool>
    {
        public InputSystemKeyInstance(InputSystemKeyAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override bool IsValidActionParameter(bool param)
        {
            return true;
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(bool param)
        {
            Key key = Action.KeyFunc.Invoke(TargetObject);
            yield return new InputSystemKeyInput(key, param);
        }
    }
}