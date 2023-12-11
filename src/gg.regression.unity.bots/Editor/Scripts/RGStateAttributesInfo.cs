using System;
using System.Collections.Generic;
using System.Linq;

namespace RegressionGames.Editor
{
    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGStateAttributeInfoWrapper
    {
        public List<RGStateAttributesInfo> RGStateAttributesInfo { get; set; }
    }
    
    [Serializable]
    public class RGStateAttributesInfo
    {
        public string ObjectType;
        public string NameSpace;
        public string ClassName;
        public List<RGStateAttributeInfo> State;
        // Whether to generate a CS file for this action, default true, but false for sample project assets
        public bool ShouldGenerateCSFile = true;
    }
}