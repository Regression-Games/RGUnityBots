using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames
{
    [System.Serializable]
    public class RGActionInfo
    {
        // the name of the component class that contains the [RGAction] attribute tags
        public string Object;
        // the name of the method tagged with [RGAction]
        public string MethodName;
        // the identifier of the action. Default is the method name, but it can be overriden.
        // ex [RGAction("Action Name"]
        public string ActionName;
        // the parameters of the method
        public List<RGParameterInfo> Parameters;
    }
}