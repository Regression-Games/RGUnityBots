using System;
using System.Collections.Generic;

namespace RegressionGames
{
    [Serializable]
    public class RGStateInfoWrapper
    {
        public List<RGStatesInfo> RGStateInfo { get; set; }
    }
    
    [Serializable]
    public class RGStatesInfo
    {
        public string Object;
        public List<RGStateInfo> State;
    }
}