using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace RegressionGames.StateActionTypes
{
    [Serializable]
    public class RGActionRequest
    {

        public readonly string Action;
        public readonly Dictionary<string, object> Input = new();

        public RGActionRequest()
        {
        }

        public RGActionRequest(string action)
        {
            this.Action = action;
        }

        public RGActionRequest(string action, Dictionary<string, object> input)
        {
            this.Action = action;
            this.Input = input;
        }

        public override string ToString()
        {
            var inputString = Input?.Select(kv => kv.Key + ": " + kv.Value).ToArray();
            return $"{{action: {Action}, input: {{{string.Join(", ", inputString ?? new string[] { })} }} }}";
        }
    }
}
