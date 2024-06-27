using System;
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
        public AnyKeyAction(string[] path, Type objectType, int actionGroup) : base(path, objectType, actionGroup)
        {
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();
        
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
            return base.IsEquivalentTo(other);
        }
    }

    public class AnyKeyInstance : RGGameActionInstance<AnyKeyAction, bool>
    {
        public AnyKeyInstance(AnyKeyAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override void PerformAction(bool param)
        {
            if (param)
            {
                // ensure at least one key is pressed (Enter)
                #if ENABLE_INPUT_SYSTEM 
                RGActionManager.SimulateKeyState(Key.Enter, true);
                #elif ENABLE_LEGACY_INPUT_MANAGER
                RGActionManager.SimulateKeyState(KeyCode.Return, true);
                #endif
            } else
            {
                // release all keys
                #if ENABLE_LEGACY_INPUT_MANAGER
                foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
                {
                    RGActionManager.SimulateKeyState(kc, false);
                }
                #endif
                #if ENABLE_INPUT_SYSTEM
                foreach (Key key in Enum.GetValues(typeof(Key)))
                {
                    RGActionManager.SimulateKeyState(key, false);
                }
                #endif
            }
        }
    }
}