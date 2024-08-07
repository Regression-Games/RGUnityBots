﻿using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// If the parameter is true, ensures that at least one key is being pressed (Enter). Otherwise, releases all keys.
    /// This is used for triggering Input.anyKey, Input.anyKeyDown, Keyboard.current.anyKey
    /// </summary>
    public class AnyKeyAction : RGGameAction
    {
        public AnyKeyAction(string[] path, Type objectType) : base(path, objectType, new RGBoolRange())
        {
        }

        public AnyKeyAction(JObject serializedAction) :
            base(serializedAction)
        {
        }

        public override string DisplayName => "Any Key";

        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new AnyKeyInstance(this, obj);
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            return other is AnyKeyAction && base.IsEquivalentTo(other);
        }

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
        }
    }

    public class AnyKeyInstance : RGGameActionInstance<AnyKeyAction, bool>
    {
        public AnyKeyInstance(AnyKeyAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override bool IsValidActionParameter(bool param)
        {
            return true;
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(bool param)
        {
            if (param)
            {
                // ensure at least one key is pressed (Enter)
                #if ENABLE_INPUT_SYSTEM 
                yield return new InputSystemKeyInput(Key.Enter, true);
                #elif ENABLE_LEGACY_INPUT_MANAGER
                yield return new LegacyKeyInput(KeyCode.Return, true);
                #endif
            } else
            {
                // release all keys
                #if ENABLE_LEGACY_INPUT_MANAGER
                foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
                {
                    yield return new LegacyKeyInput(kc, false);
                }
                #endif
                #if ENABLE_INPUT_SYSTEM
                foreach (Key key in Enum.GetValues(typeof(Key)))
                {
                    yield return new InputSystemKeyInput(key, false);
                }
                #endif
            }
        }
    }
}