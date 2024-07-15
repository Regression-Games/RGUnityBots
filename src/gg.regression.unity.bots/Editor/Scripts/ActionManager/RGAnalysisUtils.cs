using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RegressionGames.ActionManager
{
    public static class RGAnalysisUtils
    {

        public static IParameterSymbol FindArgumentParameter(ArgumentListSyntax argList, int argIndex, IMethodSymbol methodSymbol)
        {
            var parameters = methodSymbol.Parameters;
            var arg = argList.Arguments[argIndex];
            if (arg.NameColon != null)
            {
                var paramName = arg.NameColon.Name.Identifier.ValueText;
                return parameters.FirstOrDefault(param => param.Name == paramName);
            }
            else
            {
                return parameters[argIndex];
            }
        }
        
    }
}