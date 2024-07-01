﻿#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using System.Collections.Generic;
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
        public RGActionParamFunc<object> KeyCodeFunc { get; }
        
        public LegacyKeyAction(string[] path, Type objectType, RGActionParamFunc<object> keyCodeFunc) : 
            base(path, objectType)
        {
            KeyCodeFunc = keyCodeFunc;
        }

        public LegacyKeyAction(RGSerializedAction serializedAction) :
            base(serializedAction)
        {
            KeyCodeFunc = RGActionParamFunc<object>.Deserialize(serializedAction.actionFuncType, serializedAction.actionFuncData);
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
                return KeyCodeFunc == action.KeyCodeFunc;
            }
            return false;
        }

        protected override void Serialize(RGSerializedAction serializedAction)
        {
            (serializedAction.actionFuncType, serializedAction.actionFuncData) = KeyCodeFunc.Serialize();
        }
    }

    public class LegacyKeyInstance : RGGameActionInstance<LegacyKeyAction, bool>
    {
        public LegacyKeyInstance(LegacyKeyAction action, Object targetObject) : base(action, targetObject)
        {
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