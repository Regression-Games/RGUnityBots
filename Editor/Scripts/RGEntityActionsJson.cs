using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace RegressionGames.Editor
{
    [Serializable]
    // ReSharper disable InconsistentNaming
    public class RGEntityActionsJson
    {
        public string ObjectType;
        public HashSet<RGActionInfo> Actions;

        public override bool Equals(object obj)
        {
            if (obj is RGEntityActionsJson val)
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
            return $"{{ObjectType: {ObjectType}, Actions: [{string.Join(",", Actions)}]}}";
        }
    }
}
