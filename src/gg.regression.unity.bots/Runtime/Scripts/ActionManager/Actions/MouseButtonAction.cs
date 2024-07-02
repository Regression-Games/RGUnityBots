﻿using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    
    /// <summary>
    /// This is an action to press or release a particular mouse button.
    /// </summary>
    public class MouseButtonAction : RGGameAction
    {
        public RGActionParamFunc<int> MouseButtonFunc { get; }
        
        public MouseButtonAction(string[] path, Type objectType, RGActionParamFunc<int> mouseButtonFunc) 
            : base(path, objectType)
        {
            MouseButtonFunc = mouseButtonFunc;
        }

        public MouseButtonAction(RGSerializedAction serializedAction) :
            base(serializedAction)
        {
            MouseButtonFunc = RGActionParamFunc<int>.Deserialize(serializedAction.actionFuncType, serializedAction.actionFuncData);
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();

        public override string DisplayName => $"Mouse Button {MouseButtonFunc}";

        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new MouseButtonInstance(this, obj);
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is MouseButtonAction action)
            {
                return action.MouseButtonFunc == MouseButtonFunc;
            }
            else
            {
                return false;
            }
        }

        protected override void Serialize(RGSerializedAction serializedAction)
        {
            (serializedAction.actionFuncType, serializedAction.actionFuncData) = MouseButtonFunc.Serialize();
        }
    }

    public class MouseButtonInstance : RGGameActionInstance<MouseButtonAction, bool>
    {
        public MouseButtonInstance(MouseButtonAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(bool param)
        {
            var btn = Action.MouseButtonFunc.Invoke(TargetObject);
            yield return new MouseButtonInput(btn, param);
        }
    }
}