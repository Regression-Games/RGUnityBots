using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using RegressionGames.RGBotConfigs;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames
{
    public class RGActionCodeGenerator
    {
        [MenuItem("Regression Games/Generate Scripts")]
        private static void GenerateRGScripts()
        {
            //TODO: Someone/Anyone... remove this delete RGScripts directory code after November 1st, 2023... This is temporary to help devs migrate easily
            // remove old 'RGScripts' folder that is no longer used
            string dataPath = Application.dataPath;
            string directoryToDelete = Path.Combine(dataPath, "RGScripts").Replace("\\", "/");

            if (Directory.Exists(directoryToDelete))
            {
                Directory.Delete(directoryToDelete, true);
                File.Delete(directoryToDelete + ".meta");
                AssetDatabase.Refresh();
            }

            // find and extract RGAction data
            string actionJson = SearchForBotActionMethods();
            
            // find and extract RGState data
            string stateJson = SearchForBotStates();
            
            // write extracted data to json files
            WriteToJson("RGActions", actionJson);
            WriteToJson("RGStates", stateJson);

        }

        [MenuItem("Regression Games/Agent Builder/Extract Data")]
        private static void ExtractData()
        {
            ExtractObjectType();
            
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
                    var namespaceAncestors = method.Ancestors().OfType<NamespaceDeclarationSyntax>().ToArray();
                    var namespaceAncestor = namespaceAncestors.Length > 0 ? namespaceAncestors[0] : null; 
                    string nameSpace = namespaceAncestor?.Name.ToString();
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
                        Namespace = nameSpace,
                        Object = className,
                        MethodName = methodName,
                        ActionName = actionName,
                        Parameters = parameterList
                    });
                }
            }

            string jsonResult = 
                JsonConvert.SerializeObject(new RGActionsInfo { BotActions = botActionList }, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            
            // remove previous RGActions
            string dataPath = Application.dataPath;
            string directoryToDelete = Path.Combine(dataPath, "RegressionGames/Runtime/GeneratedScripts/RGActions").Replace("\\", "/");

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
                    string nameSpace = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();

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
                            Namespace = nameSpace,
                            Object = className,
                            State = stateList
                        });
                    }
                }
            }

            string jsonResult = JsonConvert.SerializeObject(new { RGStateInfo = rgStateInfoList }, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            
            // remove previous RGStates
            string dataPath = Application.dataPath;
            string directoryToDelete = Path.Combine(dataPath, "RegressionGames/Runtime/GeneratedScripts/RGStates").Replace("\\", "/");
            
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
            string folderPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "RegressionGamesZipTemp");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, $"{fileName}.json");
            File.WriteAllText(filePath, json);
        }
        
        private static string ReadFromJson(string fileName)
        {
            string folderPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "RegressionGamesZipTemp");
            string filePath = Path.Combine(folderPath, $"{fileName}.json");

            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
            else
            {
                Debug.LogWarning($"File '{fileName}.json' does not exist in '{folderPath}'.");
                return null;
            }
        }

        private static void ZipJson()
        {
            string parentPath = Directory.GetParent(Application.dataPath).FullName;
            string folderPath = Path.Combine(parentPath, "RegressionGamesZipTemp");

            if (Directory.Exists(folderPath))
            {
                string zipPath = Path.Combine(parentPath, "RegressionGames.zip");

                // Check if the zip file already exists and delete it
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
                ZipFile.CreateFromDirectory(folderPath, zipPath);

                RGDebug.LogInfo($"Successfully Generated {zipPath}");
            }
            else
            {
                Debug.LogWarning("The 'RegressionGamesZipTemp' folder does not exist.");
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

        private static void ExtractObjectType()
        {
            // read current JSON for actions and states
            string actionJson = ReadFromJson("RGActions");
            string stateJson = ReadFromJson("RGStates");
            
            // deserialize into action and state objects
            RGActionsInfo actionsInfo = JsonUtility.FromJson<RGActionsInfo>(actionJson);
            var statesInfo = JsonConvert.DeserializeObject<RGStateInfoWrapper>(stateJson).RGStateInfo;

            // all unique object names in actions and states
            List<string> objectTypeNames = new List<string>();
            
            // map of object names and object types
            Dictionary<Type, string> objectTypeMap = new Dictionary<Type, string>();
            Dictionary<string, string> objectNameMap = new Dictionary<string, string>();
            
            // Get all Object names from RGActions
            foreach (var action in actionsInfo.BotActions)
            {
                string objectName = action.Object;
                if (!objectTypeNames.Contains(objectName))
                {
                    objectTypeNames.Add(objectName);
                }
            }

            // Get all Object names from RGStates
            foreach (var state in statesInfo)
            {
                string objectName = state.Object;
                if (!objectTypeNames.Contains(objectName))
                {
                    objectTypeNames.Add(objectName);
                }
            }

            // Convert the Object name into a System.Type
            foreach (var objectName in objectTypeNames)
            {
                Type objectType = Type.GetType(objectName + ", Assembly-CSharp");
                if (objectType != null)
                {
                    objectTypeMap.TryAdd(objectType, null);
                }else
                {
                    RGDebug.LogWarning("Type not found: " + objectName);
                }
            }

            // For objects in the scene
            RGEntity[] allEntities = Object.FindObjectsOfType<RGEntity>();
            for (int i = 0; i < allEntities.Length; i++)
            {
                RGEntity entity = allEntities[i];
                var entityMap = entity.MapObjectType(objectTypeMap);
                foreach (var kvp in entityMap)
                {
                    objectTypeMap[kvp.Key] = kvp.Value;
                }
            }
            
            // For prefabs
            string[] allPrefabs = AssetDatabase.FindAssets("t:Prefab");
            foreach (string prefabGuid in allPrefabs)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
                if (prefab != null)
                {
                    RGEntity prefabComponent = prefab.GetComponent<RGEntity>();
                    if (prefabComponent != null)
                    {
                        var entityMap = prefabComponent.MapObjectType(objectTypeMap);
                        foreach (var kvp in entityMap)
                        {
                            objectTypeMap[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            
            // Convert System Type to string
            foreach (var kvp in objectTypeMap)
            {
                objectNameMap.TryAdd(kvp.Key.ToString(), kvp.Value);
            }
            
            // Assign ObjectTypes to RGActions
            foreach (var action in actionsInfo.BotActions)
            {
                if (objectNameMap.ContainsKey(action.Object))
                {
                    action.ObjectType = objectNameMap[action.Object];
                }
            }
            
            // Assign Object Types to RGStates
            foreach (var state in statesInfo)
            {
                if (objectNameMap.ContainsKey(state.Object))
                {
                    state.ObjectType = objectNameMap[state.Object];
                }
            }
            
            // Write updated values back to JSON files
            string updatedActionJson = 
                JsonConvert.SerializeObject(new RGActionsInfo { BotActions = actionsInfo.BotActions }, Formatting.Indented, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
            string updatedStateJson = JsonConvert.SerializeObject(new { RGStateInfo = statesInfo }, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            
            WriteToJson("RGActions", updatedActionJson);
            WriteToJson("RGStates", updatedStateJson);
        }
    }
}