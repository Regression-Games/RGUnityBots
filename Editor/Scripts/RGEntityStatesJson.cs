using System;
using System.Collections.Generic;

namespace RegressionGames.Editor
{
    
    [Serializable]
    // ReSharper disable InconsistentNaming
    public class RGEntityStatesJson
    {
        public string objectType;
        public HashSet<RGStateInfo> states;

        public override bool Equals(object obj)
        {
            if (obj is RGEntityStatesJson val)
            {
                return val.objectType == objectType;
            }

            return false;
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return objectType.GetHashCode();
        }

        public override string ToString()
        {
            return $"{{objectType: {objectType}, states: [{string.Join(",", states)}]}}";
        }
    }
}
