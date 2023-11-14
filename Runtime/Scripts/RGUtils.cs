using System.Collections.Generic;

namespace RegressionGames
{
    public static class RGUtils
    {
        public static bool IsCSharpPrimitive(string typeName)
        {
            HashSet<string> primitiveTypes = new HashSet<string>
            {
                "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint", "long", "ulong", "short", "ushort",
                "string"
            };

            return primitiveTypes.Contains(typeName);
        }

    }
}