#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.RGLegacyInputUtility;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action for pressing or releasing a key identified by a legacy key code identifier.
    /// </summary>
    public class LegacyKeyAction : RGGameAction
    {
        [RGActionProperty("Key Code", true)]
        public RGActionParamFunc<object> KeyCodeFunc { get; }
        
        public LegacyKeyAction(string[] path, Type objectType, RGActionParamFunc<object> keyCodeFunc) : 
            base(path, objectType, new RGBoolRange())
        {
            KeyCodeFunc = keyCodeFunc;
        }

        public LegacyKeyAction(JObject serializedAction) :
            base(serializedAction)
        {
            KeyCodeFunc = RGActionParamFunc<object>.Deserialize(serializedAction["keyCodeFunc"]);
        }

        public override string DisplayName => $"Key {KeyCodeFunc}";

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
                return KeyCodeFunc == action.KeyCodeFunc;
            }
            return false;
        }

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append(",\n\"keyCodeFunc\":");
            KeyCodeFunc.WriteToStringBuilder(stringBuilder);
        }
    }

    public class LegacyKeyInstance : RGGameActionInstance<LegacyKeyAction, bool>
    {
        public LegacyKeyInstance(LegacyKeyAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override bool IsValidActionParameter(bool param)
        {
            return true;
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(bool param)
        {
            object keyCodeOrName = Action.KeyCodeFunc.Invoke(TargetObject);
            if (keyCodeOrName is KeyCode keyCode)
            {
                yield return new LegacyKeyInput(keyCode, param);
            } else if (keyCodeOrName is string keyName)
            {
                yield return new LegacyKeyInput(RGLegacyInputWrapper.KeyNameToCode(keyName), param);
            }
            else
            {
                RGDebug.LogWarning($"Unexpected output from key code func: {keyCodeOrName}");
            }
        }
    }
}
#endif