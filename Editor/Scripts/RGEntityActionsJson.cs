using System;
using System.Collections.Generic;

namespace RegressionGames.Editor
{
    [Serializable]
    // ReSharper disable InconsistentNaming
    public class RGEntityActionsJson
    {
        public string objectType;
        public HashSet<RGActionInfo> actions;

        public override bool Equals(object obj)
        {
            if (obj is RGEntityActionsJson val)
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
            return $"{{objectType: {objectType}, actions: [{string.Join(",", actions)}]}}";
        }
    }
}
