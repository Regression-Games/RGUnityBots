using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;

namespace RegressionGames
{
    public static class GenerateRGActionClasses
    {
        public static void Generate(string jsonData)
        {
            // Load JSON input
            RGActionsInfo actionsInfo = JsonUtility.FromJson<RGActionsInfo>(jsonData);

            // Iterate through BotActions
            foreach (var botAction in actionsInfo.BotActions)
            {
                // Create a new compilation unit
                CompilationUnitSyntax compilationUnit = SyntaxFactory.CompilationUnit()
                    .AddUsings(
                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Generic")),
                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("RegressionGames")),
                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("RegressionGames.RGBotConfigs")),
                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityEngine"))
                    )
                    .AddMembers(
                        // Namespace
                        SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("RegressionGames"))
                        .AddMembers(
                            // Class declaration
                            SyntaxFactory.ClassDeclaration($"RGAction_{botAction.ActionName.Replace(" ", "_")}")
                            .AddModifiers(
                                SyntaxFactory.Token(SyntaxKind.PublicKeyword)
                                // Only add one of the "class" keywords here
                            )
                            .AddBaseListTypes(
                                SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("RGAction"))
                            )
                            .AddMembers(
                                // Start method
                                GenerateStartMethod(botAction),
                                // GetActionName method
                                GenerateGetActionNameMethod(botAction),
                                // StartAction method
                                GenerateStartActionMethod(botAction)
                            )
                        )
                    );

                // Format the generated code
                string formattedCode = compilationUnit.NormalizeWhitespace().ToFullString();
                string headerComment = "/*\n* This file has been automatically generated. Do not modify.\n*/\n\n";

                // Save to 'Assets/RGScripts/RGActions,RGSerialization.cs'
                string fileName = $"RGAction_{botAction.ActionName.Replace(" ", "_")}.cs";
                string subfolderName = Path.Combine("RGScripts", "RGActions");
                string filePath = Path.Combine(Application.dataPath, subfolderName, fileName);
                string fileContents = headerComment + formattedCode;

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, fileContents);
                RGDebug.Log($"Successfully Generated {filePath}");
                AssetDatabase.Refresh();
            }
        }

        private static MemberDeclarationSyntax GenerateStartMethod(RGActionInfo action)
        {
            var methodBody = SyntaxFactory.Block(
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                            SyntaxFactory.IdentifierName("AddMethod")
                        )
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                    new SyntaxNodeOrToken[]
                                    {
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                SyntaxFactory.Literal(action.ActionName)
                                            )
                                        ),
                                        SyntaxFactory.Token(SyntaxKind.CommaToken),
                                        GenerateActionDelegate(action)
                                    }
                                )
                            )
                        )
                )
            );

            var startMethod = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    "Start"
                )
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithBody(methodBody);

            return startMethod;
        }

        private static MemberDeclarationSyntax GenerateGetActionNameMethod(RGActionInfo action)
        {
            // Create a method body with return statement
            BlockSyntax methodBody = SyntaxFactory.Block(
                SyntaxFactory.ReturnStatement(
                    SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(action.ActionName))
                )
            );

            // Create the GetActionName method
            MethodDeclarationSyntax getActionNameMethod = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                    "GetActionName"
                )
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
                .WithBody(methodBody);

            return getActionNameMethod;
        }
        
        private static ArgumentSyntax GenerateActionDelegate(RGActionInfo action)
        {
            // Generate the GetComponent<Object>().MethodName piece for both cases (0 and non-0 parameters)
            var methodExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression($"GetComponent<{action.Object}>()"),
                SyntaxFactory.IdentifierName(action.MethodName)
            );

            if (action.Parameters.Count == 0)
            {
                // When there are no parameters, use a simple Action delegate
                var actionType = SyntaxFactory.IdentifierName("Action");

                // Create the delegate creation expression
                var delegateCreationExpression = SyntaxFactory.ObjectCreationExpression(actionType)
                    .WithArgumentList(
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(methodExpression)
                            )
                        )
                    );

                return SyntaxFactory.Argument(delegateCreationExpression);
            }
            else
            {
                // When there are parameters, create the Action delegate type with type arguments
                var actionType = SyntaxFactory.GenericName(
                        SyntaxFactory.Identifier("Action")
                    )
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SeparatedList(
                                action.Parameters.Select(param => SyntaxFactory.ParseTypeName(param.Type))
                            )
                        )
                    );

                // Create the delegate creation expression
                var delegateCreationExpression = SyntaxFactory.ObjectCreationExpression(actionType)
                    .WithArgumentList(
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(methodExpression)
                            )
                        )
                    );

                return SyntaxFactory.Argument(delegateCreationExpression);
            }
        }

        private static MemberDeclarationSyntax GenerateStartActionMethod(RGActionInfo action)
        {
            var methodParameters = new List<ParameterSyntax>
            {
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("input"))
                             .WithType(SyntaxFactory.ParseTypeName("Dictionary<string, object>"))
            };

            var parameterParsingStatements = new List<StatementSyntax>();
            var methodInvocationArguments = new List<string>();

            /*
             * Parameters have the format: {string Name; string Type}
             * 1. Extract parameter from Dictionary<string, object> passed to 'StartAction'
             * 2. Try to parse parameter into its given type
             * 3. Invoke the original method with correctly typed parameters
             */
            foreach (var parameter in action.Parameters)
            {
                string paramName = parameter.Name;
                string paramType = parameter.Type;

                methodInvocationArguments.Add(paramName);

                parameterParsingStatements.Add(SyntaxFactory.ParseStatement($"string {paramName}Input = input[\"{paramName}\"].ToString();"));
                parameterParsingStatements.Add(SyntaxFactory.ParseStatement($"{paramType} {paramName} = default;"));

                string tryParseStatement;

                if (paramType.ToLower() == "string" || paramType.ToLower() == "system.string")
                {
                    tryParseStatement = $"{paramName} = {paramName}Input;";
                }
                else if (RGUtils.IsCSharpPrimitive(paramType))
                {
                    tryParseStatement = $"{paramType}.TryParse({paramName}Input, out {paramName});";
                }
                else
                {
                    tryParseStatement = $"{paramName} = RGSerialization.Deserialize_{paramType.Replace(".", "_")}({paramName}Input);";
                }

                var tryBlock = SyntaxFactory.Block(SyntaxFactory.SingletonList<StatementSyntax>(
                    SyntaxFactory.ParseStatement(tryParseStatement)
                ));

                var catchBlock = SyntaxFactory.CatchClause()
                    .WithDeclaration(SyntaxFactory.CatchDeclaration(SyntaxFactory.ParseTypeName("Exception"), SyntaxFactory.Identifier("ex")))
                    .WithBlock(SyntaxFactory.Block(new StatementSyntax[]
                    {
                        SyntaxFactory.ParseStatement($"RGDebug.LogError(\"Failed to parse '{paramName}'\");"),
                        SyntaxFactory.ParseStatement("RGDebug.LogError(ex.Message);")
                    }));

                var tryCatchStatement = SyntaxFactory.TryStatement()
                                                     .WithBlock(tryBlock)
                                                     .WithCatches(SyntaxFactory.SingletonList(catchBlock))
                                                     .WithFinally(SyntaxFactory.FinallyClause().WithBlock(SyntaxFactory.Block()));

                parameterParsingStatements.Add(tryCatchStatement);
            }

            string methodInvocationArgumentsString = methodInvocationArguments.Count > 0 ? 
                                                     ", " + string.Join(", ", methodInvocationArguments) : 
                                                     string.Empty;

            parameterParsingStatements.Add(SyntaxFactory.ParseStatement($"Invoke(\"{action.ActionName}\"{methodInvocationArgumentsString});"));

            var methodBody = SyntaxFactory.Block(parameterParsingStatements);

            var startActionMethod = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                "StartAction"
            )
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
            .AddParameterListParameters(methodParameters.ToArray())
            .WithBody(methodBody);

            return startActionMethod;
        }

    }
}
