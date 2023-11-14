using System;

namespace RegressionGames.Editor
{
    [Serializable]
    public class RGParameterInfo
    {
        public string Name;
        public string Type;
        public bool Nullable;

        public override string ToString()
        {
            return $"{{Name: {Name}, Type: {Type}}}";
        }
    }
}