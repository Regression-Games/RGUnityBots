using System;
using System.Collections.Generic;

namespace RegressionGames.Editor
{

    [Serializable]
    // ReSharper disable InconsistentNaming
    public class RGEntityStatesJson
    {
        public string ObjectType;
        public HashSet<RGStateInfo> States;

        public override bool Equals(object obj)
        {
            if (obj is RGEntityStatesJson val)
            {
                return val.ObjectType == ObjectType;
            }

            return false;
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return ObjectType.GetHashCode();
        }

        public override string ToString()
        {
            return $"{{ObjectType: {ObjectType}, States: [{string.Join(",", States)}]}}";
        }
    }
}
