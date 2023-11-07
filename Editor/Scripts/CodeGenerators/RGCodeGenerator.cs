using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.RGBotConfigs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RegressionGames.Editor.CodeGenerators
{
    /**
     * This class takes a multi pass approach for states, but a single pass approach for actions.
     * This may change for actions in the future if we see more prevalence of writing custom action classes,
     * but for now assumes that actions are only done with [RGAction] attributes.
     *
     * State
     * 1. Find [RGState] attributes and generate classes for them
     * 2. Use RGStateEntity classes to define what is the state for AgentBuilder json.
     *      (this accounts for generated and hand written state classes)
     *
     * Actions
     * 1. Find [RGAction] attributes and generate classes for them; captures the generated class name
     * 2. /\ This same information is used as the available actions for AgentBuilder json.
     */
    public class RGCodeGenerator
    {
        // Used to exclude sample projects' directories from generation
        // so that we don't duplicate .cs files that are already included in the sample projects.
        // But while still scanning them so that we can include their States/Actions in the json.
        private static HashSet<string> _excludeDirectories = new() {
            "ThirdPersonDemoURP"
        };

        // Cache all 'asmdef' files in the project
        private static HashSet<string> _asmdefNames = new();
        
        /**
         * Collect names of all assemblies in this project
         */
        private static void CacheAssemblyNamesFromAsmdefFiles()
        {
            var asmdefFiles = Directory.GetFiles(Application.dataPath + "/../", "*.asmdef", SearchOption.AllDirectories);
            var asmdefNames = new HashSet<string>();

            foreach (var asmdefFile in asmdefFiles)
            {
                var content = JObject.Parse(File.ReadAllText(asmdefFile));
                var assemblyName = content["name"]?.ToString();
                if (!string.IsNullOrEmpty(assemblyName))
                {
                    asmdefNames.Add(assemblyName);
                }
            }

            // Add this project's build assembly and the Regression Games SDK in case they weren't already found
            asmdefNames.Add("Assembly-CSharp");
            asmdefNames.Add("RegressionGames");
            _asmdefNames = asmdefNames;
        }


        /**
         * returns a Type for the given fully qualified classname by searching each assembly until a hit is found: "{namespace}.{typeName}, {assemblyName}"
         */
        private static Type GetTypeForClassName(string classname)
        {
            if (_asmdefNames.Count < 1)
            {
                CacheAssemblyNamesFromAsmdefFiles();
            }
            Type result = null;
            foreach (var asmdefName in _asmdefNames)
            {
                result = Type.GetType(classname + ", " + asmdefName);
                if (result != null)
                {
                    break;
                }
            }

            return result;
        }
        
        [MenuItem("Regression Games/Generate Scripts")]
        private static void GenerateRGScripts()
        {
            try
            {
                // find and extract RGState data
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Searching for RGState attributes", 0.2f);
                var stateAttributesInfos = SearchForBotStateAttributes();
                // generate classes
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Generating classes for RGState attributes", 0.4f);
                GenerateStateClasses(stateAttributesInfos);

                // find and extract RGAction data
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Searching for RGAction attributes", 0.6f);
                var actionAttributeInfos = SearchForBotActionAttributes();
                // generate classes
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Generating classes for RGAction attributes", 0.8f);
                GenerateActionClasses(actionAttributeInfos);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static List<Scene> GetDirtyScenes()
        {
            var result = new List<Scene>();
            for (int j = 0; j < SceneManager.sceneCount; j++)
            {
                var scene = SceneManager.GetSceneAt(j);
                if (scene.isDirty)
                {
                    result.Add(scene);
                }
            }

            return result;
        }

        [MenuItem("Regression Games/Agent Builder/Extract Game Context")]
        private static void ExtractGameContext()
        {
            if (EditorUtility.DisplayDialog(
                    "Extract Game Context\r\nWarning",
                    "This operation will load and unload every Scene in your project's build configuration while gathering data." +
                    "\r\n\r\nOnly Scenes that are enabled in your build configuration will be evaluated." +
                    "\r\n\r\nThis operation can take a long time to complete depending on the size of your project.",
                    "Continue",
                    "Cancel"))
            {
                var dirtyScenes = GetDirtyScenes();
                if (dirtyScenes.Count > 0)
                {
                    if (EditorUtility.DisplayDialog(
                            "Unsaved Changes Detected",
                            "One or more open Scenes have unsaved changes.",
                            "Save Scenes and Continue Extract",
                            "Cancel"
                        ))
                    {
                        EditorSceneManager.SaveScenes(dirtyScenes.ToArray());
                        ExtractGameContextHelper();
                    }
                }
                else
                {
                    ExtractGameContextHelper();
                }
            }

        }
        
        private static void GenerateActionClasses(List<RGActionAttributeInfo> actionInfos)
        {
            // remove previous RGActions
            string dataPath = Application.dataPath;
            string directoryToDelete = Path.Combine(dataPath, "RegressionGames/Runtime/GeneratedScripts/RGActions").Replace("\\", "/");

            if (Directory.Exists(directoryToDelete))
            {
                Directory.Delete(directoryToDelete, true);
                File.Delete(directoryToDelete + ".meta");
                AssetDatabase.Refresh();
            }
            
            //NOTE: We may have trouble here with serialization or actionMap with sample projects
            // Much testing needed
            GenerateRGSerializationClass.Generate(actionInfos);
            GenerateRGActionClasses.Generate(actionInfos);
            GenerateRGActionMapClass.Generate(actionInfos);
        }

        private static void ExtractGameContextHelper()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Caching assembly names",
                    0.1f);
                CacheAssemblyNamesFromAsmdefFiles();
                
                // just in case they haven't done this recently or ever...
                // find and extract RGState data
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Searching for RGState attributes", 0.2f);
                var stateAttributesInfos = SearchForBotStateAttributes();
                // generate classes so that their RGStateEntity classes exist before the CreateStateInfoFromRGStateEntities step
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Generating classes for RGState attributes", 0.3f);
                GenerateStateClasses(stateAttributesInfos);

                // find and extract RGAction data
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Searching for RGAction attributes", 0.4f);
                var actionAttributeInfos = SearchForBotActionAttributes();
                // generate classes
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Generating classes for RGAction attributes", 0.5f);
                GenerateActionClasses(actionAttributeInfos);

                // Find RGStateEntity scripts and generate state info from them
                // Do NOT include the previous state infos.. so we don't have dupes
                // This gives us a consistent view across both generated and hand written state class entities
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Extracting state info rom RGStateEntity classes", 0.6f);
                var statesInfos = CreateStateInfoFromRGStateEntities();

                var actionInfos = actionAttributeInfos.Select(v => v.toRGActionInfo()).ToList();
                
                // if these have been associated to gameObjects with RGEntities, fill in their objectTypes
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Populating Object types", 0.7f);
                PopulateObjectTypes(statesInfos, actionInfos);

                // update/write the json
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Writing JSON files", 0.8f);
                WriteJsonFiles(statesInfos, actionInfos);

                // create 'RegressionGames.zip' in project folder
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Creating .zip file", 0.9f);
                CreateJsonZip();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
        
        private static List<RGActionAttributeInfo> SearchForBotActionAttributes()
        {
            // make sure to exclude any sample project directories from generation
            var excludedPaths =
                _excludeDirectories.Select(ed => Path.Combine(UnityEngine.Device.Application.dataPath, ed)).ToArray();
            
            string[] csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories).ToArray();

            List<RGActionAttributeInfo> botActionList = new List<RGActionAttributeInfo>();

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
                    string nameSpace = method.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
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
                                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                            Nullable = parameter.Type is NullableTypeSyntax || // ex. int?
                                       (parameter.Type is GenericNameSyntax && // ex. Nullable<float>
                                        ((GenericNameSyntax) parameter.Type).Identifier.ValueText == "Nullable")
                        }).ToList();

                    botActionList.Add(new RGActionAttributeInfo
                    {
                        // if this wasn't in a sample project folder, we need to generate CS for it
                        ShouldGenerateCSFile = excludedPaths.All(ep => !csFilePath.StartsWith(ep)),
                        Namespace = nameSpace,
                        Object = className,
                        MethodName = methodName,
                        ActionName = actionName,
                        Parameters = parameterList
                    });
                }
            }

            return botActionList;

        }
        
        private static void GenerateStateClasses(List<RGStateAttributesInfo> rgStateAttributesInfos)
        {
            // remove previous RGStates
            string dataPath = Application.dataPath;
            string directoryToDelete = Path.Combine(dataPath, "RegressionGames/Runtime/GeneratedScripts/RGStates").Replace("\\", "/");
            
            if (Directory.Exists(directoryToDelete))
            {
                Directory.Delete(directoryToDelete, true);
                File.Delete(directoryToDelete + ".meta");
                AssetDatabase.Refresh();
            }
            
            GenerateRGStateClasses.Generate(rgStateAttributesInfos);
        }

        private static List<RGStateAttributesInfo> SearchForBotStateAttributes()
        {
            // make sure to exclude any sample project directories from the search
            var excludedPaths =
                _excludeDirectories.Select(ed => Path.Combine(UnityEngine.Device.Application.dataPath, ed)).ToArray();
            
            string[] csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories)
                .ToArray();

            List<RGStateAttributesInfo> rgStateInfoList = new List<RGStateAttributesInfo>();

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
                    List<RGStateAttributeInfo> stateList = new List<RGStateAttributeInfo>();
                    
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

                        stateList.Add(new RGStateAttributeInfo
                        {
                            FieldType = fieldType,
                            FieldName = fieldName,
                            StateName = stateName,
                            Type = type
                        });
                    }

                    if (stateList.Any())
                    {
                        rgStateInfoList.Add(new RGStateAttributesInfo
                        {
                            ShouldGenerateCSFile = excludedPaths.All(ep => !csFilePath.StartsWith(ep)),
                            NameSpace = nameSpace,
                            ClassName = className,
                            State = stateList
                        });
                    }
                }
            }

            return rgStateInfoList;
        }
        
        private static List<RGStatesInfo> CreateStateInfoFromRGStateEntities()
        {
            var csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories).ToList();
            
            // this will find results even when the package is a local filesystem reference in the manifest.. sometimes unity does good stuff :D
            var sdkPackageCsFiles = Directory.GetFiles(Path.GetFullPath("Packages/gg.regression.unity.bots"), "*.cs", SearchOption.AllDirectories);
            
            // add files from the sdk for evaluation
            csFiles.AddRange(sdkPackageCsFiles);
            
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

                var rgStateEntityClassDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(cd => semanticModel.GetDeclaredSymbol(cd).BaseType.Name == "RGStateEntity");

                foreach (var classDeclaration in rgStateEntityClassDeclarations)
                {
                    string className = classDeclaration.Identifier.ValueText;
                    List<RGStateInfo> stateList = new List<RGStateInfo>();

                    var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as ITypeSymbol;
                    var rgStateClassName = classSymbol.BaseType.TypeArguments[0].ToDisplayString();

                    // for now we assume RGStateEntity is directly subclassed; if we allow nesting.. then we'll need to get the members from the parent type as well
                    var publicMembersAndDelegates = classDeclaration.Members
                        .Where(m => m.Modifiers.Any());

                    foreach (var member in publicMembersAndDelegates)
                    {

                        if (member is FieldDeclarationSyntax fieldDeclaration)
                        {
                            if (!fieldDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                            {
                                RGDebug.LogWarning($"Warning: Field '{fieldDeclaration.Declaration.Variables.First().Identifier.ValueText}' in class '{className}' is not public and will not be included in the available state fields.");
                                continue;
                            }
                        }
                        else if (member is PropertyDeclarationSyntax propertyDeclaration)
                        {
                            if (!propertyDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                            {
                                RGDebug.LogWarning($"Warning: Property '{propertyDeclaration.Identifier.ValueText}' in class '{className}' is not public and will not be included in the available state properties.");
                                continue;
                            }
                        }
                        else
                        {
                            // no methods
                            continue;
                        }
                        
                        string fieldName = member is PropertyDeclarationSyntax
                            ? ((PropertyDeclarationSyntax) member).Identifier.ValueText
                            : ((FieldDeclarationSyntax) member).Declaration.Variables.First().Identifier.ValueText;

                        string type = member is PropertyDeclarationSyntax
                            ? RemoveGlobalPrefix(semanticModel.GetTypeInfo(((PropertyDeclarationSyntax) member).Type)
                                .Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                            : RemoveGlobalPrefix(semanticModel.GetTypeInfo(((FieldDeclarationSyntax) member).Declaration.Type)
                                .Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                        stateList.Add(new RGStateInfo
                        {
                            StateName = fieldName,
                            Type = type
                        });
                    }

                    if (stateList.Any())
                    {
                        rgStateInfoList.Add(new RGStatesInfo
                        {
                            ClassName = rgStateClassName,
                            State = stateList
                        });
                    }
                }
            }

            return rgStateInfoList;
        }

        private static void CreateJsonZip()
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

        /**
         * WARNING/NOTE: This should be used after checking for changes in the editor or other prompting
         * to prevent users from losing their unsaved work.
         */
        private static void PopulateObjectTypes(List<RGStatesInfo> statesInfos, List<RGActionInfo> actionInfos)
        {
            
            // map of object names and object types
            Dictionary<Type, string> objectTypeMap = new Dictionary<Type, string>();
            Dictionary<string, string> objectClassNameMap = new Dictionary<string, string>();
            
            // Get all Object names from RGActions
            foreach (var action in actionInfos)
            {
                var objectType = GetTypeForClassName(action.ClassName);
                if (objectType != null)
                {
                    objectTypeMap.TryAdd(objectType, null);
                }else
                {
                    RGDebug.LogWarning($"Type not found for: {action.ClassName}");
                }
            }

            // Get all Object names from RGStates
            foreach (var state in statesInfos)
            {
                var objectType = GetTypeForClassName(state.ClassName);
                if (objectType != null)
                {
                    objectTypeMap.TryAdd(objectType, null);
                }else
                {
                    RGDebug.LogWarning($"Type not found for: {state.ClassName}");
                }
            }
            
            // iterate through all scenes rather than only the current ones in the editor
            var startingActiveScenePath = SceneManager.GetActiveScene().path;
            List<string> allActiveScenePaths = new ();
            HashSet<string> allLoadedScenePaths = new ();
            for (int j = 0; j < SceneManager.sceneCount; j++)
            {
                var scene = SceneManager.GetSceneAt(j);
                allActiveScenePaths.Add(scene.path);
                if (scene.isLoaded)
                {
                    allLoadedScenePaths.Add(scene.path);
                }
            }
            
            //sort the activeScenePaths so that the unloaded ones are at the end
            // this matters later when we reload them
            allActiveScenePaths.Sort((a, b) =>
            {
                if (allLoadedScenePaths.Contains(a))
                {
                    return -1;
                }
                return allLoadedScenePaths.Contains(b) ? 1 : 0;
            });
            
            // get all the objects in the currently open scenes.. this minimizes the amount of scene loading we have to do
            LookupEntitiesForCurrentScenes(objectTypeMap);
            
            // Get for all the other scenes in the build 
            EditorBuildSettingsScene[] scenesInBuild = EditorBuildSettings.scenes;
            foreach (var editorScene in scenesInBuild)
            {
                // include currently enabled scenes for the build
                if (editorScene.enabled && !allLoadedScenePaths.Contains(editorScene.path))
                {
                    // Open the scene 
                    EditorSceneManager.OpenScene(editorScene.path, OpenSceneMode.Single);

                    // For objects in the scene
                    LookupEntitiesForCurrentScenes(objectTypeMap);
                }
            }

            var firstReloadScene = true;
            // get the editor back to the scenes they had open before we started
            Scene? goBackToStartingActiveScene = null;
            
            foreach (var activeScenePath in allActiveScenePaths)
            {
                // open the first in singular to clear editor, then rest additive
                var mode = firstReloadScene ? OpenSceneMode.Single : (allLoadedScenePaths.Contains(activeScenePath) ? OpenSceneMode.Additive : OpenSceneMode.AdditiveWithoutLoading);
                var newScene = EditorSceneManager.OpenScene(activeScenePath, mode);
                if (newScene.path == startingActiveScenePath)
                {
                    goBackToStartingActiveScene = newScene;
                }
                firstReloadScene = false;
            }

            if (goBackToStartingActiveScene != null)
            {
                // Return back to their active starting scene
                SceneManager.SetActiveScene((Scene)goBackToStartingActiveScene);
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
                            if (kvp.Value != null)
                            {
                                objectTypeMap[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }
            }
            
            // Convert System Type to string
            foreach (var kvp in objectTypeMap)
            {
                objectClassNameMap.TryAdd(kvp.Key.ToString(), kvp.Value);
            }
            
            // Assign ObjectTypes to RGActions
            foreach (var action in actionInfos)
            {
                if (objectClassNameMap.TryGetValue(action.ClassName, out var objectType))
                {
                    action.ObjectType = objectType;
                }
            }
            
            // Assign Object Types to RGStates
            foreach (var state in statesInfos)
            {
                if (objectClassNameMap.TryGetValue(state.ClassName, out var objectType))
                {
                    state.ObjectType = objectType;
                }
            }
        }

        private static void LookupEntitiesForCurrentScenes(Dictionary<Type, string> objectTypeMap)
        {
            RGEntity[] allEntities = Object.FindObjectsOfType<RGEntity>();
            for (int i = 0; i < allEntities.Length; i++)
            {
                RGEntity entity = allEntities[i];
                var entityMap = entity.MapObjectType(objectTypeMap);
                foreach (var kvp in entityMap)
                {
                    if (kvp.Value != null)
                    {
                        objectTypeMap[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        private static void WriteJsonFiles(List<RGStatesInfo> statesInfos, List<RGActionInfo> actionInfos)
        {
            // Write values to JSON files
            string updatedActionJson = 
                JsonConvert.SerializeObject(
                    new 
                    {
                        BotActions = actionInfos
                    }, 
                    Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }
                );
            
            string updatedStateJson = 
                JsonConvert.SerializeObject(
                    new
                    {
                        //Maybe in future leave out those without an object type ??
                        //RGStateInfo = statesInfos.Where(v => !string.IsNullOrEmpty(v.ObjectType))
                        RGStateInfo = statesInfos
                    },
                    Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }
                );
            
            WriteJsonToFile("RGActions", updatedActionJson);
            WriteJsonToFile("RGStates", updatedStateJson);
        }
        
        private static void WriteJsonToFile(string fileName, string json)
        {
            string folderPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "RegressionGamesZipTemp");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, $"{fileName}.json");
            File.WriteAllText(filePath, json);
        }
    }
}