using System.Text.RegularExpressions;

namespace RegressionGames
{
    public class CodeGeneratorUtils
    {
        public static readonly string HeaderComment = $"/*\r\n* This file has been automatically generated. Do not modify.\r\n*/\r\n\r\n";
        
        public static string SanitizeActionName(string name)
        {
            return Regex.Replace(name.Replace(" ", "_"), "[^0-9a-zA-Z_]", "_");
        }
    }
}
