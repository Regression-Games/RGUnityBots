using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
using Newtonsoft.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RegressionGames
{
    public class RGActionCodeGenerator
    {
        [MenuItem("Regression Games/Generate Scripts")]
        private static void GenerateRGScripts()
        {
            // find and extract RGAction data
            string actionJson = SearchForBotActionMethods();
            
            // find and extract RGState data
            string stateJson = SearchForBotStates();
            
            // write extracted data to json files
            WriteToJson("RGActions", actionJson);
            WriteToJson("RGStates", stateJson);

            // create 'RegressionGames.zip' in project folder
            ZipJson();
        }
        
        private static string SearchForBotActionMethods()
        {
            string[] csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains("Library") && !path.Contains("Temp"))
                .ToArray();

            List<RGActionInfo> botActionList = new List<RGActionInfo>();

            foreach (string csFilePath in csFiles)
            {
                string scriptText = File.ReadAllText(csFilePath);

                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(scriptText);

                var compilation = CSharpCompilation.Create("RGCompilation")
                    .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                    .AddSyntaxTrees(syntaxTree);

                CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();

                var botActionMethods = root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(method =>
                        method.AttributeLists.Any(attrList =>
                            attrList.Attributes.Any(attr =>
                                attr.Name.ToString() == "RGAction")))
                    .ToList();

                foreach (var method in botActionMethods)
                {
                    string className = method.Ancestors().OfType<ClassDeclarationSyntax>().First().Identifier.ValueText;
                    string methodName = method.Identifier.ValueText;

                    string actionName = methodName;
                    var actionAttribute = method.AttributeLists.SelectMany(attrList => attrList.Attributes)
                        .FirstOrDefault(attr => attr.Name.ToString() == "RGAction");

                    if (actionAttribute != null)
                    {
                        var attributeArgument = actionAttribute.ArgumentList?.Arguments.FirstOrDefault();
                        if (attributeArgument != null &&
                            attributeArgument.Expression is LiteralExpressionSyntax literal)
                        {
                            actionName = literal.Token.ValueText;
                        }
                    }

                    var semanticModel = compilation.GetSemanticModel(syntaxTree);
                    var parameterList = method.ParameterList.Parameters.Select(parameter =>
                        new RGParameterInfo
                        {
                            Name = parameter.Identifier.ValueText,
                            Type = RemoveGlobalPrefix(semanticModel.GetTypeInfo(parameter.Type).Type
                                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        }).ToList();

                    botActionList.Add(new RGActionInfo
                    {
                        Object = className,
                        MethodName = methodName,
                        ActionName = actionName,
                        Parameters = parameterList
                    });
                }
            }

            string jsonResult =
                JsonConvert.SerializeObject(new RGActionsInfo { BotActions = botActionList }, Formatting.Indented);
            
            // remove previous RGActions
            string dataPath = Application.dataPath;
            string directoryToDelete = Path.Combine(dataPath, "RGScripts/RGActions").Replace("\\", "/");

            if (Directory.Exists(directoryToDelete))
            {
                Directory.Delete(directoryToDelete, true);
                File.Delete(directoryToDelete + ".meta");
                AssetDatabase.Refresh();
            }
            
            GenerateRGSerializationClass.Generate(jsonResult);
            GenerateRGActionClasses.Generate(jsonResult);
            GenerateRGActionMapClass.Generate(jsonResult);

            return jsonResult;
        }

        private static string SearchForBotStates()
        {
            string[] csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains("Library") && !path.Contains("Temp"))
                .ToArray();

            List<RGStatesInfo> rgStateInfoList = new List<RGStatesInfo>();

            foreach (string csFilePath in csFiles)
            {
                string scriptText = File.ReadAllText(csFilePath);

                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(scriptText);

                var compilation = CSharpCompilation.Create("RGCompilation")
                    .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                    .AddSyntaxTrees(syntaxTree);

                CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);

                var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var classDeclaration in classDeclarations)
                {
                    string className = classDeclaration.Identifier.ValueText;
                    List<RGStateInfo> stateList = new List<RGStateInfo>();

                    var membersWithRGState = classDeclaration.Members
                        .Where(m => m.AttributeLists.Any(a => a.Attributes.Any(attr => attr.Name.ToString() == "RGState")));

                    foreach (var member in membersWithRGState)
                    {
                        bool hasError = false;
                        
                        if (member is FieldDeclarationSyntax fieldDeclaration)
                        {
                            if (!fieldDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                            {
                                RGDebug.LogError($"Error: Field '{fieldDeclaration.Declaration.Variables.First().Identifier.ValueText}' in class '{className}' is not public.");
                                hasError = true;
                            }
                        }
                        else if (member is MethodDeclarationSyntax methodDeclaration)
                        {
                            if (!methodDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                            {
                                RGDebug.LogError($"Error: Method '{methodDeclaration.Identifier.ValueText}' in class '{className}' is not public.");
                                hasError = true;
                            }
                            else if (methodDeclaration.ParameterList.Parameters.Count > 0)
                            {
                                RGDebug.LogError($"Error: Method '{methodDeclaration.Identifier.ValueText}' in class '{className}' has parameters, which is not allowed.");
                                hasError = true;
                            }
                            else if (methodDeclaration.ReturnType is PredefinedTypeSyntax predefinedType && predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword))
                            {
                                RGDebug.LogError($"Error: Method '{methodDeclaration.Identifier.ValueText}' in class '{className}' has a void return type, which is not allowed.");
                                hasError = true;
                            }
                        }

                        if (hasError)
                        {
                            continue;
                        }
                        
                        string fieldType = member is MethodDeclarationSyntax ? "method" : "variable";
                        string fieldName = member is MethodDeclarationSyntax
                            ? ((MethodDeclarationSyntax) member).Identifier.ValueText
                            : ((FieldDeclarationSyntax) member).Declaration.Variables.First().Identifier.ValueText;

                        string stateName = fieldName;
                        var attribute = member.AttributeLists.SelectMany(attrList => attrList.Attributes)
                            .FirstOrDefault(attr => attr.Name.ToString() == "RGState");

                        if (attribute != null)
                        {
                            var attributeArgument = attribute.ArgumentList?.Arguments.FirstOrDefault();
                            if (attributeArgument != null &&
                                attributeArgument.Expression is LiteralExpressionSyntax literal)
                            {
                                stateName = literal.Token.ValueText;
                            }
                        }

                        string type = member is MethodDeclarationSyntax
                            ? RemoveGlobalPrefix(semanticModel.GetTypeInfo(((MethodDeclarationSyntax) member).ReturnType)
                                .Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                            : RemoveGlobalPrefix(semanticModel.GetTypeInfo(((FieldDeclarationSyntax) member).Declaration.Type)
                                .Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                        stateList.Add(new RGStateInfo
                        {
                            FieldType = fieldType,
                            FieldName = fieldName,
                            StateName = stateName,
                            Type = type
                        });
                    }

                    if (stateList.Any())
                    {
                        rgStateInfoList.Add(new RGStatesInfo
                        {
                            Object = className,
                            State = stateList
                        });
                    }
                }
            }

            string jsonResult = JsonConvert.SerializeObject(new { RGStateInfo = rgStateInfoList }, Formatting.Indented);
            
            // remove previous RGStates
            string dataPath = Application.dataPath;
            string directoryToDelete = Path.Combine(dataPath, "RGScripts/RGStates").Replace("\\", "/");
            
            if (Directory.Exists(directoryToDelete))
            {
                Directory.Delete(directoryToDelete, true);
                File.Delete(directoryToDelete + ".meta");
                AssetDatabase.Refresh();
            }
            
            GenerateRGStateClasses.Generate(jsonResult);
            return jsonResult;
        }

        private static void WriteToJson(string fileName, string json)
        {
            string folderPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "RegressionGames");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, $"{fileName}.json");
            File.WriteAllText(filePath, json);
        }

        private static void ZipJson()
        {
            string parentPath = Directory.GetParent(Application.dataPath).FullName;
            string folderPath = Path.Combine(parentPath, "RegressionGames");

            if (Directory.Exists(folderPath))
            {
                string zipPath = Path.Combine(parentPath, "RegressionGames.zip");
                ZipFile.CreateFromDirectory(folderPath, zipPath);
                Directory.Delete(folderPath, true);
            }
            else
            {
                Debug.LogWarning("The 'RegressionGames' folder does not exist.");
            }
        }
        
        private static string RemoveGlobalPrefix(string typeName)
        {
            return typeName.Replace("global::", string.Empty);
        }

        private static string GetFullTypeNameWithNamespace(ITypeSymbol typeSymbol, SemanticModel semanticModel)
        {
            if (typeSymbol == null)
                return "UnknownType";

            INamespaceSymbol containingNamespace = typeSymbol.ContainingNamespace;
            string namespacePrefix = containingNamespace.ToDisplayString();

            if (!string.IsNullOrEmpty(namespacePrefix))
                namespacePrefix += ".";

            return namespacePrefix + typeSymbol.Name;
        }
    }
}