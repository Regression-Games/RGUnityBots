using System;
using System.Collections.Generic;

namespace RegressionGames.Editor
{
    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGStateInfoWrapper
    {
        public List<RGStatesInfo> RGStatesInfo { get; set; }
    }
    
    [Serializable]
    public class RGStatesInfo
    {
        public string EntityTypeName;
        public string ClassName;
        public List<RGStateInfo> States;
    }
}