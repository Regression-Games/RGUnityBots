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
        public string ClassName;
        public string AssemblyName;
        // the object type, given by the developer
        public string ObjectType;
        public List<RGStateInfo> State;
    }
}