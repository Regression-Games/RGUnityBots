using System;

namespace RegressionGames.Editor
{
    [Serializable]
    public class RGStateInfo
    {
        public string StateName;
        public string Type;


        public override bool Equals(object obj)
        {
            if (obj is RGStateInfo value)
            {
                return StateName == value.StateName;
            }

            return false;
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return StateName.GetHashCode();
        }

        public override string ToString()
        {
            return $"{{StateName: {StateName}, Type: {Type}}}";
        }
    }
}