using System.Text.RegularExpressions;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RegressionGames
{
#if UNITY_EDITOR
    public class CodeGeneratorUtils
    {
        public static readonly string HeaderComment = $"/*\r\n* This file has been automatically generated. Do not modify.\r\n*/\r\n\r\n";

        public static string SanitizeActionName(string name)
        {
            return Regex.Replace(name.Replace(" ", "_"), "[^0-9a-zA-Z_]", "_");
        }

        /**
         * Returns a sanitized name to use as a namespace for generated code in this project
         */
        public static string GetNamespaceForProject()
        {
            return Regex.Replace("RG" + PlayerSettings.productName.Replace(" ", "_"), "[^0-9a-zA-Z_]", "_");
        }
    }
#endif
}
