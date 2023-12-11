using System;
using RegressionGames.StateActionTypes;

namespace RegressionGames.RGBotConfigs
{
    // ReSharper disable once InconsistentNaming
    //While this can be subclassed by users, its real purpose is for agent builder code generation
    public abstract class RGState_Button : RGState
    {
        protected override Type GetTypeForStateEntity()
        {
            return typeof(RGStateEntity_Button);
        }
    }

}