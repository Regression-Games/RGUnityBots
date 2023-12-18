using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RegressionGames.StateActionTypes;
#endif

// ReSharper disable once CheckNamespace
// ReSharper disable InconsistentNaming
namespace RegressionGames.Editor.CodeGenerators
{
    /**
     * This class takes a multi pass approach for States, but a single pass approach for Actions.
     * This may change for Actions in the future if we see more prevalence of writing custom action classes,
     * but for now assumes that Actions are only done with [RGAction] attributes.
     *
     * State
     * 1. Find [RGState] attributes and generate classes for them
     * 2. Use RGStateEntity classes to define what is the state for AgentBuilder json.
     *      (this accounts for generated and hand written state classes)
     *
     * Actions
     * 1. Find [RGAction] attributes and generate classes for them; captures the generated class name
     * 2. /\ This same information is used as the available Actions for AgentBuilder json.
     */

    public static class RGCodeGenerator
    {
#if UNITY_EDITOR
        // Used to exclude sample projects' directories from generation
        // so that we don't duplicate .cs files that are already included in the sample projects.
        // But while still scanning them so that we can include their States/Actions in the json.
        private static readonly HashSet<string> ExcludeDirectories = new() {
           "ThirdPersonDemoURP"
        };

        private static bool _hasExtractProblem;
        
        private static readonly DirectoryInfo ParentDirectory = Directory.GetParent(Application.dataPath);

        private static void RecordError(string error)
        {
            _hasExtractProblem = true;
            RGDebug.LogError($"ERROR: {error}");
        }

        private static void RecordWarning(string warning)
        {
            _hasExtractProblem = true;
            RGDebug.LogWarning($"WARNING: {warning}");
        }

        [MenuItem("Regression Games/Generate Scripts")]
        public static void GenerateRGScripts()
        {
            try
            {
                _hasExtractProblem = false;

                // cleanup old RGStateEntity classes wherever they live
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Cleaning up previously generated RGStateEntity classes", 0.1f);
                CleanupPreviousRGStateEntityClasses();
                
                // generate new RGStateEntity classes
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Generating new RGStateEntity classes", 0.3f);
                GenerateRGStateEntityClasses();

                // find and extract RGAction data
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Searching for [RGAction] attributes", 0.5f);
                var actionAttributeInfos = SearchForBotActionAttributes();
                
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Cleaning up previously generated RGActions classes", 0.7f);
                CleanupPreviousRGStateEntityClasses();
                
                // generate classes
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Generating new RGActions classes", 0.9f);
                GenerateActionClasses(actionAttributeInfos);

                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (_hasExtractProblem)
            {
                RGDebug.LogWarning($"Completed generating Regression Games scripts - Errors occurred, check logs above...");
                EditorUtility.DisplayDialog(
                    "Generate Scripts\r\nError",
                    "One or more warnings or errors occurred while generating Regression Games scripts." +
                    "\r\n\r\nCheck the Console logs for more information.",
                    "OK");
                _hasExtractProblem = false;
            }
            else
            {
                RGDebug.LogInfo($"Completed generating Regression Games scripts");
            }

        }

        private static List<Scene> GetDirtyScenes()
        {
            var result = new List<Scene>();
            for (var j = 0; j < SceneManager.sceneCount; j++)
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
                    "Extract Game Context",
                    "This operation will load and unload every Scene in your project's build configuration while gathering data." +
                    "\r\n\r\nOnly Scenes that are enabled in your build configuration will be evaluated." +
                    "\r\n\r\nThis operation can take a long time to complete depending on the size of your project.",
                    "Continue",
                    "Cancel"))
            {
                _hasExtractProblem = false;
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
                if (_hasExtractProblem)
                {
                    RGDebug.LogWarning($"Completed extracting Regression Games context - Errors occurred, check logs above...");
                    EditorUtility.DisplayDialog(
                        "Extract Game Context\r\nError",
                        "One or more warnings or errors occurred during the extract." +
                        "\r\n\r\nCheck the Console logs for more information.",
                        "OK");
                    _hasExtractProblem = false;
                }
                else
                {
                    var zipPath = Path.Combine(ParentDirectory.FullName,  "RegressionGames.zip");
                        RGDebug.LogInfo($"Completed extracting Regression Games context - filePath: {zipPath}");
                        EditorUtility.DisplayDialog(
                            "Extract Game Context\r\nComplete",
                            "Game context extracted to .zip file:" +
                            $"\r\n\r\n{zipPath}",
                            "OK");
                }
            }

        }

        private static void GenerateActionClasses(List<RGActionAttributeInfo> actionInfos)
        {
            var fileWriteTasks = new List<(string,Task)>();
            var actionInfosByBehaviour = actionInfos
                .GroupBy(v => (v.BehaviourNamespace, v.BehaviourName, v.BehaviourFileDirectory))
                .ToDictionary(v => v.Key, v => v.ToList());
            foreach (var (behaviourDetails,actionInfoList) in actionInfosByBehaviour)
            {
                var newFileName = $"Generated_RGActions_{behaviourDetails.BehaviourName}.cs";
                fileWriteTasks.Add((newFileName,
                        GenerateRGActionsClass.Generate(
                            behaviourDetails.BehaviourFileDirectory+Path.DirectorySeparatorChar+newFileName,
                            behaviourDetails.BehaviourName,
                            behaviourDetails.BehaviourNamespace,
                            actionInfoList
                            )
                        )
                    );
            }

            Task.WaitAll(fileWriteTasks.Select(v => v.Item2).ToArray());
            foreach (var (filename, _) in fileWriteTasks)
            {
                RGDebug.Log($"Successfully created: {filename}");
            }
                
        }

        private static void ExtractGameContextHelper()
        {
            try
            {
                // just in case they haven't done this recently or ever...
                // find and extract RGState data
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data",
                    "Cleaning up previously generated RGStateEntity classes", 0.1f);
                CleanupPreviousRGStateEntityClasses();
                
                // generate new RGStateEntity classes
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Generating new RGStateEntity classes", 0.2f);
                GenerateRGStateEntityClasses();
                
                // find and extract RGAction data
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data",
                    "Searching for RGAction attributes", 0.3f);
                var actionAttributeInfos = SearchForBotActionAttributes();
                
                // generate classes
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data",
                    "Cleaning up previously generated RGActions classes", 0.4f);
                CleanupPreviousRGActionsClasses();
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data",
                    "Generating new RGActions classes", 0.5f);
                GenerateActionClasses(actionAttributeInfos);

                if (_hasExtractProblem)
                {
                    // if the code generation phase failed.. don't waste any more time
                    return;
                }

                // Find RGStateEntity scripts and generate state info from them
                // Do NOT include the previous state infos.. so we don't have dupes
                // This gives us a consistent view across both generated and hand written state class entities
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data",
                    "Extracting state info rom RGStateEntity classes", 0.6f);
                var statesInfos = CreateStateInfoFromRGStateEntities();
                
                // Do the same for generate RGActionRequest scripts
                var actionInfos = CreateActionInfoFromRGActionRequests();

                if (_hasExtractProblem)
                {
                    return;
                }

                // if these have been associated to gameObjects with RGEntities, fill in their objectTypes
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data",
                    "Populating Object types", 0.7f);
                var stateAndActionJsonStructure = CreateStateAndActionJsonWithObjectTypes(statesInfos, actionInfos);

                // update/write the json
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data",
                    "Writing JSON files", 0.8f);
                WriteJsonFiles(stateAndActionJsonStructure.Item1.ToList(), stateAndActionJsonStructure.Item2.ToList());

                // create 'RegressionGames.zip' in project folder
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data",
                    "Creating .zip file", 0.9f);
                CreateJsonZip();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
        
        private static IEnumerable<Assembly> GetAssemblies()
        {
            var list = new List<string>();
            var stack = new Stack<Assembly>();

            stack.Push(Assembly.GetEntryAssembly());

            do
            {
                var asm = stack.Pop();

                yield return asm;

                foreach (var reference in asm.GetReferencedAssemblies())
                    if (!list.Contains(reference.FullName))
                    {
                        stack.Push(Assembly.Load(reference));
                        list.Add(reference.FullName);
                    }

            }
            while (stack.Count > 0);

        }

        private static List<RGActionAttributeInfo> SearchForBotActionAttributes()
        {
            // make sure to exclude any sample project directories from generation, but not searching
            var excludedPaths =
                ExcludeDirectories.Select(ed => Path.Combine(UnityEngine.Device.Application.dataPath, ed)).ToArray();

            var csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories).ToArray();

            var botActionList = new List<RGActionAttributeInfo>();

            foreach (var csFilePath in csFiles)
            {
                var scriptText = File.ReadAllText(csFilePath);

                var syntaxTree = CSharpSyntaxTree.ParseText(scriptText);

                var compilation = CSharpCompilation.Create("RGCompilation")
                    .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                    .AddSyntaxTrees(syntaxTree);

                var root = syntaxTree.GetCompilationUnitRoot();

                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                
                var monoBehaviourClassDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Where(cd => semanticModel.GetDeclaredSymbol(cd).BaseType.Name == "MonoBehaviour");
                
                foreach (var classDeclaration in monoBehaviourClassDeclarations)
                {
                    var behaviourName
                        = classDeclaration.Identifier.ValueText;
                    var nameSpace = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()
                        ?.Name.ToString();
                    var botActionMethods = classDeclaration.Members
                        .OfType<MethodDeclarationSyntax>()
                        .Where(method =>
                            method.AttributeLists.Any(attrList =>
                                attrList.Attributes.Any(attr =>
                                    attr.Name.ToString() == "RGAction")))
                        .ToList();
                    
                    var classModel = (ITypeSymbol)semanticModel.GetDeclaredSymbol(classDeclaration);
                    var rgStateTypeAttribute = classModel.GetAttributes().FirstOrDefault(a => a.AttributeClass.Name == "RGStateTypeAttribute");

                    var entityTypeName = behaviourName;
                    
                    if (rgStateTypeAttribute != null)
                    {

                        // do logic for all the fields based on the type attribute settings
                        foreach (var (key, value) in rgStateTypeAttribute.NamedArguments)
                        {
                            switch (key)
                            {
                                case "typeName":
                                    entityTypeName = value.ToString();
                                    break;
                            }
                        }
                    }
                    
                    foreach (var method in botActionMethods)
                    {
                        var methodName = method.Identifier.ValueText;

                        var actionName = methodName;
                        var actionAttribute = method.AttributeLists.SelectMany(attrList => attrList.Attributes)
                            .FirstOrDefault(attr => attr.Name.ToString() == "RGAction");

                        var attributeArgument = actionAttribute?.ArgumentList?.Arguments.FirstOrDefault();
                        if (attributeArgument is { Expression: LiteralExpressionSyntax literal })
                        {
                            actionName = literal.Token.ValueText;
                        }
                        
                        var parameterList = method.ParameterList.Parameters.Select(parameter =>
                            new RGParameterInfo
                            {
                                Name = parameter.Identifier.ValueText,
                                Type = RemoveGlobalPrefix(semanticModel.GetTypeInfo(parameter.Type).Type
                                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                                Nullable = parameter.Type is NullableTypeSyntax || // ex. int?
                                           (parameter.Type is GenericNameSyntax syntax && // ex. Nullable<float>
                                            syntax.Identifier.ValueText == "Nullable")
                            }).ToList();

                        botActionList.Add(new RGActionAttributeInfo
                        {
                            BehaviourFileDirectory = csFilePath.Substring(0, csFilePath.LastIndexOf(Path.PathSeparator)),
                            // if this wasn't in a sample project folder, we need to generate CS for it
                            ShouldGenerateCSFile = excludedPaths.All(ep => !csFilePath.StartsWith(ep)),
                            BehaviourNamespace = nameSpace,
                            BehaviourName = behaviourName,
                            MethodName = methodName,
                            ActionName = actionName,
                            Parameters = parameterList,
                            EntityTypeName = entityTypeName
                        });
                    }
                }
            }

            return botActionList;

        }

        private static void CleanupPreviousFilesWithPathAndPattern(string path, string searchPattern)
        {
            var excludedPaths =
                ExcludeDirectories.Select(ed => Path.Combine(UnityEngine.Device.Application.dataPath, ed)).ToArray();
            
            // find all .cs files that match our pattern and remove them
            var filesToRemove = Directory.EnumerateFiles(path, searchPattern, SearchOption.AllDirectories).Where(csFilePath => excludedPaths.All(ep => !csFilePath.StartsWith(ep)));
            
            foreach (var filePath in filesToRemove)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch (Exception)
                {
                    // didn't remove file, but it probably didn't exist
                    // may need to really handle this later when writing
                }

                var metaFilePath = filePath.Substring(0, filePath.Length - 3) + ".meta";
                try
                {
                    if (File.Exists(metaFilePath))
                    {
                        File.Delete(metaFilePath);
                    }
                }
                catch (Exception)
                {
                    // didn't remove file, but it probably didn't exist
                    // may need to really handle this later when writing
                }
            }
        }
        
        private static void CleanupPreviousRGActionsClasses()
        {
            CleanupPreviousFilesWithPathAndPattern(Application.dataPath, "*Generated_RGActions_*.cs");
        }

        private static void CleanupPreviousRGStateEntityClasses()
        {
            CleanupPreviousFilesWithPathAndPattern(Application.dataPath, "*Generated_RGStateEntity_*.cs");
        }

        public class StateBehaviourPropertyInfo
        {
            public bool IsMethod;
            public string StateName;
            public string FieldName;
            public string Type;
        }

        private static StateBehaviourPropertyInfo GetStateNameFieldNameAndTypeForMember(bool hasRGStateAttribute, SemanticModel semanticModel, string className, MemberDeclarationSyntax member, RGStateTypeAttribute.RGStateIncludeFlags includeFlags)
        {
            if (member is FieldDeclarationSyntax fieldDeclaration)
            {
                // follow exclusion rules for fields unless they have [RGState] explicitly
                if (!hasRGStateAttribute && (includeFlags & RGStateTypeAttribute.RGStateIncludeFlags.Field) == 0)
                {
                    return null;
                }
                
                if (!fieldDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                {
                    RecordError($"Error: Field '{fieldDeclaration.Declaration.Variables.First().Identifier.ValueText}' in class '{className}' is not public.");
                    return null;
                }
            }
            else if (member is MethodDeclarationSyntax methodDeclaration)
            {
                // follow exclusion rules for methods unless they have [RGState] explicitly
                if (!hasRGStateAttribute && (includeFlags & RGStateTypeAttribute.RGStateIncludeFlags.Method) == 0)
                {
                    return null;
                }
                
                if (!methodDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                {
                    RecordError($"Error: Method '{methodDeclaration.Identifier.ValueText}' in class '{className}' is not public.");
                    return null;
                }
                
                if (methodDeclaration.ParameterList.Parameters.Count > 0)
                {
                    RecordError($"Error: Method '{methodDeclaration.Identifier.ValueText}' in class '{className}' has parameters, which is not allowed.");
                    return null;
                }
                
                if (methodDeclaration.ReturnType is PredefinedTypeSyntax predefinedType && predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword))
                {
                    RecordError($"Error: Method '{methodDeclaration.Identifier.ValueText}' in class '{className}' has a void return type, which is not allowed.");
                    return null;
                }
            }
            else if (member is PropertyDeclarationSyntax propertyDeclaration)
            {
                // follow exclusion rules for properties unless they have [RGState] explicitly
                if (!hasRGStateAttribute && (includeFlags & RGStateTypeAttribute.RGStateIncludeFlags.Property) == 0)
                {
                    return null;
                }
                
                if (!propertyDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                {
                    RecordError(
                        $"Error: Property '{propertyDeclaration.Identifier.ValueText}' in class '{className}' is not public.");
                    return null;
                }
            }
            
            string fieldName;
            string type;
            var isMethod = false;
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    fieldName = field.Declaration.Variables.First().Identifier.ValueText;
                    type = RemoveGlobalPrefix(semanticModel.GetTypeInfo(field.Declaration.Type)
                        .Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    break;
                case PropertyDeclarationSyntax property:
                    fieldName = property.Identifier.ValueText;
                    type = RemoveGlobalPrefix(semanticModel
                        .GetTypeInfo(property.Type)
                        .Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    break;
                case MethodDeclarationSyntax method:
                    isMethod = true;
                    fieldName = method.Identifier.ValueText;
                    type = RemoveGlobalPrefix(semanticModel
                        .GetTypeInfo(method.ReturnType)
                        .Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    break;
                default:
                    RecordError(
                        $"Error: [RGState] attribute in class '{className}' is applied to an invalid declaration: {member}.");
                    return null;
            }

            var stateName = fieldName;
            var attribute = member.AttributeLists.SelectMany(attrList => attrList.Attributes)
                .FirstOrDefault(attr => attr.Name.ToString() == "RGState");

            var attributeArgument = attribute?.ArgumentList?.Arguments.FirstOrDefault();
            if (attributeArgument is { Expression: LiteralExpressionSyntax literal })
            {
                stateName = literal.Token.ValueText;
            }

            return new StateBehaviourPropertyInfo()
            {
                IsMethod = isMethod,
                FieldName = fieldName,
                StateName = stateName,
                Type = type
            };
        }

        private static void GenerateRGStateEntityClasses()
        {
            // make sure to exclude any sample project directories from the search
            var excludedPaths =
                ExcludeDirectories.Select(ed => Path.Combine(UnityEngine.Device.Application.dataPath, ed)).ToArray();

            var csFiles = Directory.EnumerateFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories).Where(csFilePath => excludedPaths.All(ep => !csFilePath.StartsWith(ep)));

            var fileWriteTasks = new List<(string,Task)>();
            // for each .cs file in the project
            foreach (var csFilePath in csFiles)
            {
                var scriptText = File.ReadAllText(csFilePath);

                var syntaxTree = CSharpSyntaxTree.ParseText(scriptText);

                var compilation = CSharpCompilation.Create("RGCompilation")
                    .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                    .AddSyntaxTrees(syntaxTree);

                var root = syntaxTree.GetCompilationUnitRoot();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);

                var monoBehaviourClassDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Where(cd => semanticModel.GetDeclaredSymbol(cd).BaseType.Name == "MonoBehaviour");
                
                // for each class declared in this .cs file
                foreach (var classDeclaration in monoBehaviourClassDeclarations)
                {
                    var nameSpace = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
                    var behaviourName = classDeclaration.Identifier.ValueText;

                    var classModel = (ITypeSymbol)semanticModel.GetDeclaredSymbol(classDeclaration);
                    var rgStateTypeAttribute = classModel.GetAttributes().FirstOrDefault(a => a.AttributeClass.Name == "RGStateTypeAttribute");

                    var membersWithRGStateAttribute = classDeclaration.Members
                        .Where(m =>
                            m.AttributeLists.Any(a =>
                                a.Attributes.Any(attr =>
                                    attr.Name.ToString() == "RGState"
                                    )
                                )
                            )
                        .ToHashSet();
                    
                    var isPlayer = false;
                    var entityTypeName = behaviourName;
                    var includeFlags = RGStateTypeAttribute.DefaultFlags;

                    if (rgStateTypeAttribute != null)
                    {

                        // do logic for all the fields based on the type attribute settings
                        foreach (var (key, value) in rgStateTypeAttribute.NamedArguments)
                        {
                            switch (key)
                            {
                                case "typeName":
                                    entityTypeName = value.ToString();
                                    break;
                                case "isPlayer":
                                    isPlayer = bool.Parse(value.ToString());
                                    break;
                                case "includeFlags":
                                    //TODO: Parse these correctly
                                    break;
                            }
                        }
                    }

                    var hasRGStateAttributes = membersWithRGStateAttribute.Count > 0;

                    var stateMetadata = new List<StateBehaviourPropertyInfo>();
                    if (hasRGStateAttributes)
                    {
                        // do logic for just those attributes
                        foreach (var member in membersWithRGStateAttribute)
                        {
                            var nextEntry = GetStateNameFieldNameAndTypeForMember(true, semanticModel, behaviourName, member, includeFlags);
                            stateMetadata.Add(nextEntry);
                        }
                    }
                    else if (rgStateTypeAttribute != null)
                    {
                        // do logic for all the fields based on the type attribute settings
                        var includeMembers = classDeclaration.Members
                            .Where(m =>
                                // if it has [RGState explicitly], or is NOT obsolete
                                // iow.. obsolete members come through if annotated with [RGState]
                                membersWithRGStateAttribute.Contains(m)
                                || !m.AttributeLists.Any(a =>
                                    a.Attributes.Any(attr =>
                                        attr.Name.ToString() == "ObsoleteAttribute"
                                        )
                                    )
                                );
                        
                        foreach (var member in includeMembers)
                        {
                            var hasRGState = membersWithRGStateAttribute.Contains(member);
                            var nextEntry = GetStateNameFieldNameAndTypeForMember(hasRGState, semanticModel, behaviourName, member, includeFlags);
                            stateMetadata.Add(nextEntry);
                        }
                    }
                    else
                    {
                        // do nothing.. had no RGState related things
                    }

                    if (stateMetadata.Count > 0)
                    {
                        var fileDirectory = csFilePath.Substring(0, csFilePath.LastIndexOf(Path.PathSeparator));
                        var newFileName = $"Generated_RGStateEntity_{behaviourName}.cs";
                        fileWriteTasks.Add((newFileName, GenerateRGStateEntityClass.Generate(fileDirectory + Path.PathSeparator + newFileName, entityTypeName, isPlayer, behaviourName, nameSpace, stateMetadata)));
                    }
                }
            }
            
            Task.WaitAll(fileWriteTasks.Select(v=>v.Item2).ToArray());
            foreach (var (filename,_) in fileWriteTasks)
            {
                RGDebug.Log($"Successfully created: {filename}");
            }

        }

        private static Dictionary<string,List<RGActionInfo>> CreateActionInfoFromRGActionRequests()
        {
            var actionInfos = new Dictionary<string, List<RGActionInfo>>();
            // get all classes of type RGActionRequest and add them
            var loadedAndReferencedAssemblies = GetAssemblies();
            var rgActionRequestTypes = loadedAndReferencedAssemblies.SelectMany(a => a.GetTypes())
                .Where(t => t.IsSubclassOf(typeof(RGActionRequest)));
            foreach (var rgActionRequestType in rgActionRequestTypes)
            {
                var actionRequest = (RGActionRequest)Activator.CreateInstance(rgActionRequestType);
                var entityTypeName = actionRequest.GetEntityType();
                if (!actionInfos.TryGetValue(entityTypeName, out var theList))
                {
                    theList = new List<RGActionInfo>();
                    actionInfos[entityTypeName] = theList;
                }

                var inList = theList.FirstOrDefault(v => v.ActionName == actionRequest.Action) != null;
                if (!inList)
                {
                    // get the parameters by interrogating the constructor args
                    // this assumes that there is only one named constructor for these custom RGActionRequest classes
                    var namedConstructors = actionRequest.GetType().GetConstructors()
                        .Where(v => v.Name == actionRequest.GetType().Name).ToList();

                    if (namedConstructors.Count < 1)
                    {
                        RecordError(
                            $"RGActionRequest class: {actionRequest.GetType().FullName} does not define the required single named constructor with arguments");
                        break;
                    }

                    if (namedConstructors.Count > 1)
                    {
                        RecordError(
                            $"RGActionRequest class: {actionRequest.GetType().FullName} defines multiple named constructors with arguments, but only a single constructor is allowed");
                        break;
                    }

                    var constructorArgs = namedConstructors[0].GetParameters();

                    // add an entry for this hand written rgActionRequest
                    theList.Add(new RGActionInfo()
                        {
                            GeneratedRGActionRequestName = actionRequest.GetType().FullName,
                            ActionName = actionRequest.Action,
                            Parameters = constructorArgs.Select(v => new RGParameterInfo()
                            {
                                Name = v.Name,
                                Type = v.ParameterType.FullName,
                                Nullable = Nullable.GetUnderlyingType(v.ParameterType) != null
                            }).ToList()
                        }
                    );
                }
                else
                {
                    RecordError(
                        $"Multiple RGActionRequest classes specify action: {actionRequest.Action} on the same entityType: {entityTypeName}");
                    break;
                }
            }

            return actionInfos;
        }


        private static List<RGStatesInfo> CreateStateInfoFromRGStateEntities()
        {
            var result = new List<RGStatesInfo>(); 
            var loadedAndReferencedAssemblies = GetAssemblies();
            var rgStateEntityTypes = loadedAndReferencedAssemblies.SelectMany(a => a.GetTypes())
                .Where(t => t.IsSubclassOf(typeof(IRGStateEntity)));
            foreach (var rgStateEntityType in rgStateEntityTypes)
            {
                var stateEntity = (RGActionRequest)Activator.CreateInstance(rgStateEntityType);
                var entityTypeName = stateEntity.GetEntityType();
                // all RGStateEntity accessors are public properties (=> impls)
                var className = rgStateEntityType.FullName;
                var properties = rgStateEntityType.GetMembers(BindingFlags.Public).Where(v => v.MemberType == MemberTypes.Property);
                var stateList = (
                    from memberInfo
                    in properties
                    where memberInfo.DeclaringType != null
                    select new RGStateInfo { StateName = memberInfo.Name, Type = memberInfo.DeclaringType.FullName }
                    ).ToList();
                
                if (stateList.Any())
                {
                    result.Add(new RGStatesInfo
                    {
                        EntityTypeName = entityTypeName,
                        ClassName = className,
                        States = stateList
                    });
                }
            }

            return result;
        }

        private static void CreateJsonZip()
        {
            var parentPath = ParentDirectory.FullName;
            var folderPath = Path.Combine(parentPath, "RegressionGamesZipTemp");

            if (Directory.Exists(folderPath))
            {
                var zipPath = Path.Combine(parentPath, "RegressionGames.zip");

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
        
        private static (List<RGEntityStatesJson>, List<RGEntityActionsJson>) CreateStateAndActionJsonWithObjectTypes(
            List<RGStatesInfo> statesInfos, Dictionary<string, List<RGActionInfo>> actionInfos)
        {

            var statesJson = statesInfos.Select(v => new RGEntityStatesJson()
            {
                ObjectType = v.EntityTypeName,
                States = v.States.ToHashSet()
            }).ToList();

            var actionsJson = actionInfos.Select(v => new RGEntityActionsJson()
            {
                ObjectType = v.Key,
                Actions =  v.Value.ToHashSet()
            }).ToList();
            
            statesJson.Sort((a,b) => String.Compare(a.ObjectType, b.ObjectType, StringComparison.Ordinal));
            actionsJson.Sort((a,b) => String.Compare(a.ObjectType, b.ObjectType, StringComparison.Ordinal));
            return (statesJson,actionsJson);
        }

        private static void WriteJsonFiles(List<RGEntityStatesJson> statesInfos, List<RGEntityActionsJson> actionInfos)
        {
            // Write values to JSON files
            var updatedActionJson =
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

            var updatedStateJson =
                JsonConvert.SerializeObject(
                    new
                    {
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
            var folderPath = Path.Combine(ParentDirectory.FullName, "RegressionGamesZipTemp");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var filePath = Path.Combine(folderPath, $"{fileName}.json");
            File.WriteAllText(filePath, json);
        }
#endif
    }

}
