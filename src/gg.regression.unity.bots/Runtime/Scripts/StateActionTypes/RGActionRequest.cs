using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace RegressionGames.StateActionTypes
{
    [Serializable]
    public class RGActionRequest
    {
        public string action;
        [CanBeNull] public Dictionary<string, object> Input;

        public RGActionRequest()
        {

        }

        public RGActionRequest(string action, Dictionary<string, object> input)
        {
            this.action = action;
            this.Input = input;
        }

        public override string ToString()
        {
            var inputString = Input?.Select(kv => kv.Key + ": " + kv.Value).ToArray();
            return $"{{action: {action}, input: {{{string.Join(", ", inputString ?? new string[] { })} }} }}";
        }
    }
}
