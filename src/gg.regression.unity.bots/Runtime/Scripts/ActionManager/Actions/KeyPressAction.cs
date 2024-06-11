using System;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.Types;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    public class KeyPressAction : RGGameAction
    {
        private RGKeyCode _keyCode; 
        
        public KeyPressAction(string path, Type objectType, RGKeyCode keyCode) : base(path, objectType)
        {
            _keyCode = keyCode;
        }

        public override IRGValueRange ParameterRange => new RGBoolRange(false, true);
        
        public override RGGameActionInstance GetInstance(Object obj)
        {
            throw new NotImplementedException();
        }
    }

    public class KeyPressActionInstance : RGGameActionInstance
    {
        public KeyPressActionInstance(RGGameAction action, Object instance) : base(action, instance)
        {
        }

        public override void Perform(object param)
        {
            bool paramVal = (bool)param;
        }
    }
}