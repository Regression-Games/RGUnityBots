using System.Text.RegularExpressions;

namespace RegressionGames
{
    public class CodeGeneratorUtils
    {
        public static string SanitizeActionName(string name)
        {
            return Regex.Replace(name.Replace(" ", "_"), "[^0-9a-zA-Z_]", "_");
        }
    }
}
