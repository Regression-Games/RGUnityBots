using System;
using System.Collections.Generic;

namespace RegressionGames.Editor
{
    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGActionInfo
    {
        // the namespace of the class that contains the [RGAction] attribute tags
        public string Namespace;
        // the name of the component class that contains the [RGAction] attribute tags
        public string Object;
        // the object type, given by the developer
        public string ObjectType;
        // the name of the method tagged with [RGAction]
        public string MethodName;
        // the identifier of the action. Default is the method name, but it can be overriden.
        // ex [RGAction("Action Name"]
        public string ActionName;
        // the parameters of the method
        public List<RGParameterInfo> Parameters;
    }
}