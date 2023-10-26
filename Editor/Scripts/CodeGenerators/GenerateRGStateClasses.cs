using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace RegressionGames.Editor.CodeGenerators
{
    public static class GenerateRGStateClasses
    {
        public static void Generate(string jsonInput)
        {
            var rgStateInfos = JsonConvert.DeserializeObject<RGStateInfoWrapper>(jsonInput).RGStateInfo;

            foreach (var rgStateInfo in rgStateInfos)
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

                if (!string.IsNullOrEmpty(rgStateInfo.Namespace))
                {
                    usings.Add(rgStateInfo.Namespace);
                }
                
                var className = $"{rgStateInfo.Object}_RGState";
                var componentType = rgStateInfo.Object;

                // Create a new compilation unit
                var compilationUnit = SyntaxFactory.CompilationUnit()
                    .AddUsings(
                        usings.Select(v=>SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(v))).ToArray()
                    );
                
                // Create a new class declaration with the desired name
                var classDeclaration = SyntaxFactory.ClassDeclaration(className)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("RGState")));

                // Create the private field
                var fieldDeclaration = SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(componentType))
                            .AddVariables(SyntaxFactory.VariableDeclarator("myComponent")))
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

                // Create the Start method
                var startMethod = GenerateStartMethod(componentType, rgStateInfo.State);

                // Create the GetState method
                var getStateMethod = GenerateGetStateMethod(componentType, rgStateInfo.State);

                // Add the members to the class declaration
                classDeclaration = classDeclaration
                    .AddMembers(fieldDeclaration, startMethod, getStateMethod);

                // Create namespace
                var namespaceDeclaration = SyntaxFactory
                    .NamespaceDeclaration(SyntaxFactory.ParseName("RegressionGames.RGBotConfigs"))
                    .AddMembers(classDeclaration);

                // Add the namespace declaration to the compilation unit
                compilationUnit = compilationUnit.AddMembers(namespaceDeclaration);

                // Get the full code text
                var formattedCode = compilationUnit.NormalizeWhitespace().ToFullString();

                // Write the code to a .cs file
                // Save to 'Assets/RegressionGames/Runtime/GeneratedScripts/RGStates/{name}.cs'
                string subfolderName = Path.Combine("RegressionGames", "Runtime", "GeneratedScripts", "RGStates");
                string fileName = $"{className}.cs";
                string filePath = Path.Combine(Application.dataPath, subfolderName, fileName);
                string fileContents = CodeGeneratorUtils.HeaderComment + formattedCode;
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, fileContents);
                RGDebug.Log($"Successfully Generated {filePath}");
                AssetDatabase.Refresh();
            }
        }

        private static MethodDeclarationSyntax GenerateStartMethod(string componentType, List<RGStateInfo> stateInfos)
        {
            var startMethod = SyntaxFactory
                .MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "Start")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithBody(SyntaxFactory.Block());

            // Check for component
            startMethod = startMethod.AddBodyStatements(
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName("myComponent"),
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.ThisExpression(),
                                SyntaxFactory.GenericName("GetComponent")
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                SyntaxFactory.IdentifierName(componentType)
                                            )
                                        )
                                    )
                            )
                        )
                    )
                )
            );

            // Add null check statement
            startMethod = startMethod.AddBodyStatements(
                SyntaxFactory.IfStatement(
                    SyntaxFactory.BinaryExpression(
                        SyntaxKind.EqualsExpression,
                        SyntaxFactory.IdentifierName("myComponent"),
                        SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
                    ),
                    SyntaxFactory.Block(
                        SyntaxFactory.SingletonList<StatementSyntax>(
                            SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.InvocationExpression(
                                        SyntaxFactory.IdentifierName("RGDebug.LogError")
                                    )
                                    .WithArgumentList(
                                        SyntaxFactory.ArgumentList(
                                            SyntaxFactory.SingletonSeparatedList(
                                                SyntaxFactory.Argument(
                                                    SyntaxFactory.LiteralExpression(
                                                        SyntaxKind.StringLiteralExpression,
                                                        SyntaxFactory.Literal($"{componentType} not found")
                                                    )
                                                )
                                            )
                                        )
                                    )
                            )
                        )
                    )
                )
            );

            return startMethod;
        }

        private static MethodDeclarationSyntax GenerateGetStateMethod(string componentType, List<RGStateInfo> memberInfos)
        {
            var statements = new List<StatementSyntax>
            {
                // Create a new dictionary
                SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName("var"))
                        .AddVariables(SyntaxFactory.VariableDeclarator("state")
                            .WithInitializer(SyntaxFactory.EqualsValueClause(
                                SyntaxFactory
                                    .ObjectCreationExpression(SyntaxFactory.ParseTypeName("Dictionary<string, object>"))
                                    .WithArgumentList(SyntaxFactory.ArgumentList())))))
            };

            // Add statements to add each state variable to the dictionary
            statements.AddRange(memberInfos.Select(mi =>
            {
                ExpressionSyntax valueExpression = mi.FieldType == "method" 
                    ? SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("myComponent"),
                            SyntaxFactory.IdentifierName(mi.FieldName)
                        )
                    )
                    : SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("myComponent"),
                        SyntaxFactory.IdentifierName(mi.FieldName)
                    );

                return SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("state"),
                                SyntaxFactory.IdentifierName("Add")
                            )
                        )
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList(
                                    new[]
                                    {
                                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(mi.StateName))),
                                        SyntaxFactory.Argument(valueExpression)
                                    }
                                )
                            )
                        )
                );
            }));

            // Add statement to return the dictionary
            statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("state")));

            // Create the GetState method
            var getStateMethod = SyntaxFactory
                .MethodDeclaration(SyntaxFactory.ParseTypeName("Dictionary<string, object>"), "GetState")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
                    SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
                .WithBody(SyntaxFactory.Block(statements));

            return getStateMethod;
        }

    }
}