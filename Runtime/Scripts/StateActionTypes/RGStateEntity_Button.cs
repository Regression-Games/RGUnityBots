using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using RegressionGames.RGBotConfigs;
using UnityEngine;

namespace RegressionGames.StateActionTypes
{
    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGStateEntity_Button : RGStateEntity<RGState>
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
