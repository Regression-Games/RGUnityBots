using System;
using System.Collections.Generic;

namespace RegressionGames.Editor
{
    [Serializable]
    public class RGStateInfoWrapper
    {
        public List<RGStatesInfo> RGStateInfo { get; set; }
    }
    
    [Serializable]
    public class RGStatesInfo
    {
        public string AssemblyName;
        public string Namespace;
        public string Object;
        // the object type, given by the developer
        public string ObjectType;
        public List<RGStateInfo> State;
    }
}