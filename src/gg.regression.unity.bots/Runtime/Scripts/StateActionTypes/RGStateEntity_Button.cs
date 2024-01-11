using System;
using System.Collections.Generic;

namespace RegressionGames.StateActionTypes
{
    // ReSharper disable InconsistentNaming
    [Serializable]
    // DEV WARNING, This type argument must be the fully qualified `RegressionGames.RGBotConfigs.RGState` or the CodeGenerator will not get the classname of the type argument fully qualified correctly
    public class RGStateEntity_Button : RGStateEntity<RegressionGames.RGBotConfigs.RGState_Button>
    {
        // is this button currently interactable
        public bool interactable => (bool)this.GetValueOrDefault("interactable", false);

        // This is mostly implemented to make visibility in the debugger much easier... especially when finding the right object in the overall state
        public override string ToString()
        {
            return $"{base.ToString()} , interactable: {interactable}";
        }
    }

}
