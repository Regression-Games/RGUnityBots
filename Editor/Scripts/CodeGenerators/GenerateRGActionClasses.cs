using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace RegressionGames.Editor.CodeGenerators
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
                HashSet<string> usings = new()
                {
                    "System",
                    "System.Collections.Generic",
                    "RegressionGames",
                    "RegressionGames.RGBotConfigs",
                    "RegressionGames.StateActionTypes",
                    "UnityEngine"
                };

                if (!string.IsNullOrEmpty(botAction.Namespace))
                {
                    usings.Add(botAction.Namespace);
                }
                
                // Create a new compilation unit
                CompilationUnitSyntax compilationUnit = SyntaxFactory.CompilationUnit()
                    .AddUsings(
                        usings.Select(v=>SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(v))).ToArray()
                    )
                    .AddMembers(
                        // Namespace
                        SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("RegressionGames"))
                        .AddMembers(
                            // Class declaration
                            SyntaxFactory.ClassDeclaration($"RGAction_{CodeGeneratorUtils.SanitizeActionName(botAction.ActionName)}")
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
                                ),
                            SyntaxFactory.ClassDeclaration($"RGActionRequest_{CodeGeneratorUtils.SanitizeActionName(botAction.ActionName)}")
                                .AddModifiers(
                                    SyntaxFactory.Token(SyntaxKind.PublicKeyword)
                                    // Only add one of the "class" keywords here
                                )
                                .AddBaseListTypes(
                                    SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("RGActionRequest"))
                                ).AddMembers(
                                    // Action Request method
                                    GenerateActionRequestConstructor(botAction)
                                )
                        )
                    );

                // Format the generated code
                string formattedCode = compilationUnit.NormalizeWhitespace().ToFullString();

                // Save to 'Assets/RegressionGames/Runtime/GeneratedScripts/RGActions,RGSerialization.cs'
                string fileName = $"RGAction_{CodeGeneratorUtils.SanitizeActionName(botAction.ActionName)}.cs";
                string subfolderName = Path.Combine("RegressionGames", "Runtime", "GeneratedScripts", "RGActions");
                string filePath = Path.Combine(Application.dataPath, subfolderName, fileName);
                string fileContents = CodeGeneratorUtils.HeaderComment + formattedCode;

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

            foreach (var parameter in action.Parameters)
            {
                string paramName = parameter.Name;
                
                methodInvocationArguments.Add(paramName);
                parameterParsingStatements.Add(SyntaxFactory.ParseStatement($"{parameter.Type} {paramName} = default;"));
                parameterParsingStatements.Add(SyntaxFactory.IfStatement(IfCondition(parameter), IfBody(parameter), ElseBody(parameter)));
            }

            string methodInvocationArgumentsString = methodInvocationArguments.Count > 0 ? 
                                                     ", " + string.Join(", ", methodInvocationArguments) : 
                                                     string.Empty;

            parameterParsingStatements.Add(SyntaxFactory.ParseStatement($"Invoke(\"{action.ActionName}\"{methodInvocationArgumentsString});"));

            return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                "StartAction"
            )
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
            .AddParameterListParameters(methodParameters.ToArray())
            .WithBody(SyntaxFactory.Block(parameterParsingStatements));
        }

        /**
         * input.TryGetValue("targetId", out var targetIdInput)
         */
        private static InvocationExpressionSyntax IfCondition(RGParameterInfo param)
        {
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression, 
                    SyntaxFactory.IdentifierName("input"), 
                    SyntaxFactory.IdentifierName("TryGetValue")
                    )).WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new List<ArgumentSyntax> 
                    { 
                        SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression, 
                                SyntaxFactory.Literal(param.Name)
                            )
                        ),
                        SyntaxFactory.Argument(
                            SyntaxFactory.DeclarationExpression(
                                    SyntaxFactory.IdentifierName("var"),
                                    SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier($"{param.Name}Input")))
                            ).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.OutKeyword))
                    })
                ));
        }

        /**
         * try
         * {
         *      string:
         *      key = keyInput.ToString();
         * 
         *      primitive:
         *      KeyType.TryParse(keyInput.ToString(), out key);
         *
         *      nonprimitive:
         *      key = RGSerialization.Deserialize_KeyType(key.ToString());
         * 
         * }
         * catch (Exception ex)
         * {
         *      RGDebug.LogError("Failed to parse 'skillId'");
         *      RGDebug.LogError(ex.Message);
         * }
         */
        private static StatementSyntax IfBody(RGParameterInfo param)
        {
            var paramType = param.Type;
            var paramName = param.Name;
            
            string tryParseStatement;
            if (paramType.ToLower() == "string" || paramType.ToLower() == "system.string")
            {
                tryParseStatement = $"{paramName} = {paramName}Input.ToString();";
            }
            else if (RGUtils.IsCSharpPrimitive(paramType))
            {
                tryParseStatement = $"{paramType}.TryParse({paramName}Input.ToString(), out {paramName});";
            }
            else
            {
                // Do direct type cast whenever possible, taking into account nullable types 
                tryParseStatement = $"if ({paramName}Input is {paramType.Replace("?", "")}";
                if (param.Nullable)
                {
                    tryParseStatement += " or null";
                }
                tryParseStatement += ")";
                tryParseStatement += $"{{ {paramName} = ({paramType}){paramName}Input; }}";
                tryParseStatement += $"\r\nelse {{ {paramName} = RGSerialization.Deserialize_{paramType.Replace(".", "_").Replace("?", "")}";

                if (param.Nullable)
                {
                    tryParseStatement += "_Nullable";
                }
                tryParseStatement += $"({paramName}Input.ToString());";

                tryParseStatement += "}";
            }

            var tryBlock = SyntaxFactory.Block(SyntaxFactory.SingletonList(
                SyntaxFactory.ParseStatement(tryParseStatement)
            ));

            var catchBlock = SyntaxFactory.CatchClause()
                .WithDeclaration(SyntaxFactory.CatchDeclaration(SyntaxFactory.ParseTypeName("Exception"), SyntaxFactory.Identifier("ex")))
                .WithBlock(SyntaxFactory.Block(new StatementSyntax[]
                {
                    SyntaxFactory.ParseStatement($"RGDebug.LogError($\"Failed to parse '{paramName}' - {{ex}}\");"),
                }));

            return SyntaxFactory.Block(
                SyntaxFactory.TryStatement()
                    .WithBlock(tryBlock)
                    .WithCatches(SyntaxFactory.SingletonList(catchBlock))
            );
        }

        /**
         * RGDebug.LogError("No parameter 'key' found");
         * return;
         */
        private static ElseClauseSyntax ElseBody(RGParameterInfo param)
        {
            if (param.Nullable)
            {
                return default(ElseClauseSyntax);
            }
            
            // Validation check for key existence if param must be non-null
            return SyntaxFactory.ElseClause(SyntaxFactory.Block(new StatementSyntax[] 
                    {
                        SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression, 
                                        SyntaxFactory.IdentifierName("RGDebug"), 
                                        SyntaxFactory.IdentifierName("LogError")
                                )
                            ).WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.LiteralExpression(
                                                SyntaxKind.StringLiteralExpression, 
                                                SyntaxFactory.Literal($"No parameter '{param.Name}' found")
                                                )
                                            )
                                        )
                                    )
                                )
                            ), SyntaxFactory.ReturnStatement() 
                    }
                ));
        }

        private static MemberDeclarationSyntax GenerateActionRequestConstructor(RGActionInfo action)
        {
            var methodParameters = new List<ParameterSyntax>();
            var parameterParsingStatements = new List<StatementSyntax>();
            
            foreach (var rgParameterInfo in action.Parameters)
            {
                methodParameters.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier(rgParameterInfo.Name))
                    .WithType(SyntaxFactory.ParseTypeName(rgParameterInfo.Type)));
            }

            parameterParsingStatements.Add(SyntaxFactory.ParseStatement($"action = \"{action.ActionName}\";"));

            var inputString = "Input = new () {";
            foreach (var rgParameterInfo in action.Parameters)
            {
                inputString += $"\r\n {{ \"{rgParameterInfo.Name}\", {rgParameterInfo.Name} }},";
            }

            inputString += "\r\n};";
            
            parameterParsingStatements.Add(
                SyntaxFactory.ParseStatement(inputString)
            );

            var methodBody = SyntaxFactory.Block(parameterParsingStatements);

            var constructor = SyntaxFactory.ConstructorDeclaration(
                $"RGActionRequest_{CodeGeneratorUtils.SanitizeActionName(action.ActionName)}"
            )
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(methodParameters.ToArray())
            .WithBody(methodBody);

            return constructor;
        }

    }
}
