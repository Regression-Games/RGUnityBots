using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace RegressionGames.Editor.CodeGenerators
{
    public static class GenerateRGSerializationClass
    {
        public static void Generate(string jsonData)
        {
            // Parse JSON and extract parameter types
            List<RGActionInfo> botActions = ParseJson(jsonData);

            // Create a namespace and class declaration
            NamespaceDeclarationSyntax namespaceDeclaration = SyntaxFactory
                .NamespaceDeclaration(SyntaxFactory.ParseName("RegressionGames"))
                .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityEngine")))
                .AddMembers(GenerateClass(botActions));

            // Create a compilation unit and add the namespace declaration
            CompilationUnitSyntax compilationUnit = SyntaxFactory.CompilationUnit()
                .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")))
                .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Newtonsoft.Json")))
                .AddMembers(namespaceDeclaration);

            // Format the generated code
            string formattedCode = compilationUnit.NormalizeWhitespace().ToFullString();

            string headerComment = "/*\n* This file has been automatically generated. Do not modify.\n*/\n\n";

            // Save to 'Assets/RegressionGames/Runtime/GeneratedScripts/RGSerialization.cs'
            string fileName = "RGSerialization.cs";
            string filePath = Path.Combine(Application.dataPath, "RegressionGames", "Runtime", "GeneratedScripts", fileName);
            string fileContents = headerComment + formattedCode;
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, fileContents);
            RGDebug.Log($"Successfully Generated {filePath}");
            AssetDatabase.Refresh();
        }

        private static ClassDeclarationSyntax GenerateClass(List<RGActionInfo> botActions)
        {
            // Generate methods for deserialization based on parameter types
            List<MemberDeclarationSyntax> methodDeclarations = new List<MemberDeclarationSyntax>();
            HashSet<string> processedTypes = new HashSet<string>();

            foreach (RGActionInfo botAction in botActions)
            {
                foreach (RGParameterInfo parameter in botAction.Parameters)
                {
                    if (RGUtils.IsCSharpPrimitive(parameter.Type))
                    {
                        continue;
                    }
                    
                    if (!processedTypes.Contains(parameter.Type))
                    {
                        processedTypes.Add(parameter.Type);

                        /*
                         * Generates a method called Deserialize_{Type} for every non-primitive type
                         * Ex: Vector3
                         * public static Vector3 Deserialize_Vector3(string paramJson)
                         *    return JsonConvert.DeserializeObject<Vector3>(paramJson);
                         */
                        MethodDeclarationSyntax method = SyntaxFactory
                            .MethodDeclaration(SyntaxFactory.ParseTypeName(parameter.Type), GetDeserializerMethodName(parameter))
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                                SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                            .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("paramJson"))
                                .WithType(SyntaxFactory.ParseTypeName("string")))
                            .WithBody(SyntaxFactory.Block(SyntaxFactory.ReturnStatement(
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.ParseTypeName("JsonConvert"),
                                        SyntaxFactory.GenericName(SyntaxFactory.Identifier("DeserializeObject"))
                                            .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(
                                                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                    SyntaxFactory.ParseTypeName(parameter.Type))))),
                                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName("paramJson"))))))));

                        methodDeclarations.Add(method);
                    }
                }
            }

            // Create the class declaration
            ClassDeclarationSyntax classDeclaration = SyntaxFactory.ClassDeclaration("RGSerialization")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .AddMembers(methodDeclarations.ToArray());

            return classDeclaration;
        }

        private static string GetDeserializerMethodName(RGParameterInfo parameter)
        {
            var result = $"Deserialize_{parameter.Type.Replace(".", "_")}";
            if (parameter.Nullable)
            {
                result = result.Replace("?", "");
                result += "_Nullable";
            }
            return result;
        }
        
        // Convert jsonData to RGActionsInfo
        private static List<RGActionInfo> ParseJson(string jsonData)
        {
            var parsedData = JsonUtility.FromJson<RGActionsInfo>(jsonData);
            return parsedData.BotActions;
        }
    }
}