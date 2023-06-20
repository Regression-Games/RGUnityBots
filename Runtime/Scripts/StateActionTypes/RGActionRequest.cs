using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace RegressionGames.StateActionTypes
{
    [Serializable]
    public class RGActionRequest
    {
        public string action;
        [CanBeNull] public Dictionary<string, object> input;
    }
}
