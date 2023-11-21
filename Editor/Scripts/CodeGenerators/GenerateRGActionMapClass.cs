using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
#endif

namespace RegressionGames.Editor.CodeGenerators
{
#if UNITY_EDITOR
    // Dev Note: Not perfect, but mega time saver for generating this gook: https://roslynquoter.azurewebsites.net/
    public static class GenerateRGActionMapClass
    {
        public static void Generate(List<RGActionAttributeInfo> botActions)
        {

            HashSet<string> usings = new()
            {
                "UnityEngine"
            };
            foreach (var rgActionInfo in botActions)
            {
                if (!string.IsNullOrEmpty(rgActionInfo.Namespace))
                {
                    usings.Add(rgActionInfo.Namespace);
                }
            }

            // Create a namespace and class declaration
            NamespaceDeclarationSyntax namespaceDeclaration = SyntaxFactory
                .NamespaceDeclaration(SyntaxFactory.ParseName(CodeGeneratorUtils.GetNamespaceForProject()))
                .AddUsings(
                    usings.Select(v=>SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(v))).ToArray()
                )
                .AddMembers(GenerateClass(botActions));


            // Create a compilation unit and add the namespace declaration
            CompilationUnitSyntax compilationUnit = SyntaxFactory.CompilationUnit()
                .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")))
                .AddMembers(namespaceDeclaration);

            // Format the generated code
            string formattedCode = compilationUnit.NormalizeWhitespace(eol: Environment.NewLine).ToFullString();

            // Save to 'Assets/RegressionGames/Runtime/GeneratedScripts/RGActionMap.cs'
            string fileName = "RGActionMap.cs";
            string filePath = Path.Combine(Application.dataPath, "RegressionGames", "Runtime", "GeneratedScripts", fileName);
            string fileContents = CodeGeneratorUtils.HeaderComment + formattedCode;
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, fileContents);
            RGDebug.Log($"Successfully Generated {filePath}");
            AssetDatabase.Refresh();
        }
        private static ClassDeclarationSyntax GenerateClass(List<RGActionAttributeInfo> botActions)
        {
            var methodsList = new List<MethodDeclarationSyntax>();

            var filteredActions = botActions.Where(b => b.ShouldGenerateCSFile).ToList();

            var startMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "Awake")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                .WithBody(SyntaxFactory.Block(filteredActions
                    .GroupBy(b => b.Object)
                    .SelectMany(g =>
                        {
                            var innerIfStatements = g.Select(b =>
                                SyntaxFactory.ExpressionStatement(
                                    SyntaxFactory.InvocationExpression(
                                        SyntaxFactory.MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            SyntaxFactory.IdentifierName("gameObject"),
                                            SyntaxFactory.IdentifierName($"AddComponent<RGAction_{CodeGeneratorUtils.SanitizeActionName(b.ActionName)}>")
                                        )
                                    )
                                )
                            ).ToArray();

                            return new StatementSyntax[]
                            {
                                SyntaxFactory.IfStatement(
                                    SyntaxFactory.InvocationExpression(
                                        SyntaxFactory.MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            SyntaxFactory.ThisExpression(),
                                            SyntaxFactory.GenericName("TryGetComponent")
                                                .WithTypeArgumentList(
                                                    SyntaxFactory.TypeArgumentList(
                                                        SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                            SyntaxFactory.IdentifierName(g.Key)
                                                        )
                                                    )
                                                )
                                        ),
                                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                                            SyntaxFactory.Argument(
                                                null,
                                                SyntaxFactory.Token(SyntaxKind.OutKeyword),
                                                SyntaxFactory.DeclarationExpression(
                                                    SyntaxFactory.IdentifierName("var"),
                                                    SyntaxFactory.DiscardDesignation(SyntaxFactory.Token(SyntaxKind.UnderscoreToken))
                                                )
                                            )
                                        ))
                                    ),
                                    SyntaxFactory.Block(innerIfStatements)
                                )
                            };
                        }
                    ).ToArray()));

            methodsList.Add(startMethod);

            var classDeclaration = SyntaxFactory.ClassDeclaration("RGActionMap")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName("MonoBehaviour")))
                .AddMembers(methodsList.ToArray());

            return classDeclaration;
        }

    }
#endif
}
