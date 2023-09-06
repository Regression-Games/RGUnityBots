using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames
{
    [System.Serializable]
    public class RGActionInfo
    {
        public string Object;
        public string MethodName;
        public string ActionName; // Added this field
        public List<RGParameterInfo> Parameters;
    }
}