using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames
{
    [System.Serializable]
    public class RGStateInfoWrapper
    {
        public List<RGStatesInfo> RGStateInfo { get; set; }
    }
    
    [System.Serializable]
    public class RGStatesInfo
    {
        public string Object;
        public List<RGStateInfo> State;
    }
}