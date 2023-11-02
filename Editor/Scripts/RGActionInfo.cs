using System;
using System.Collections.Generic;

namespace RegressionGames.Editor
{
    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGActionInfo
    {
        // the name of the RGAction class
        public string ClassName;
        // the object type, given by the developer
        public string ObjectType;
        // the identifier of the action. Default is the method name, but it can be overriden.
        // ex [RGAction("Action Name"]
        public string ActionName;
        // the parameters of the method
        public List<RGParameterInfo> Parameters;
    }
}