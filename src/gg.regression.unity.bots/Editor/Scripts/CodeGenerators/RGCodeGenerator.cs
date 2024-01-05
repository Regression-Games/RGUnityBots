using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
#if UNITY_EDITOR
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
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

        [MenuItem("Regression Games/Regenerate All Scripts")]
        public static void GenerateRGScripts()
        {
            try
            {
                RGDebug.LogInfo($"Generating Regression Games scripts...");
                _hasExtractProblem = false;

                // cleanup old RGStateEntity classes wherever they live
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Cleaning up previously generated RGStateEntity classes", 0.1f);
                RGDebug.LogDebug($"Cleaning up previously generated RGStateEntity classes...");
                CleanupPreviousRGStateEntityClasses();
                
                // generate new RGStateEntity classes
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Generating new RGStateEntity classes", 0.3f);
                RGDebug.LogDebug($"Generating new RGStateEntity classes...");
                GenerateRGStateEntityClasses();

                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Cleaning up previously generated RGActions classes", 0.5f);
                RGDebug.LogDebug($"Cleaning up previously generated RGActions classes...");
                CleanupPreviousRGActionsClasses();
                
                // find and extract RGAction data
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Searching for [RGAction] attributes", 0.7f);
                RGDebug.LogDebug($"Searching for [RGAction] attributes...");
                var actionAttributeInfos = SearchForBotActionAttributes();
                
                // generate classes
                EditorUtility.DisplayProgressBar("Generating Regression Games Scripts",
                    "Generating new RGActions classes", 0.9f);
                RGDebug.LogDebug($"Generating new RGActions classes...");
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

        [MenuItem("Regression Games/Agent Builder/Extract Game Context")]
        private static void ExtractGameContext()
        {
            _hasExtractProblem = false;
                
            // don't need to generate scripts here because we auto do that on any code change
            try
            {
                RGDebug.LogInfo($"Extracting Regression Games context...");
                
                // Find RGStateEntity scripts and generate state info from them
                // Do NOT include the previous state infos.. so we don't have dupes
                // This gives us a consistent view across both generated and hand written state class entities
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data",
                    "Extracting state info from RGStateEntity classes", 0.2f);
                var statesInfos = CreateStateInfoFromRGStateEntities();
                
                // Do the same for generate RGActionRequest scripts
                var actionInfos = CreateActionInfoFromRGActionRequests();

                if (_hasExtractProblem)
                {
                    return;
                }

                // if these have been associated to gameObjects with RGEntities, fill in their objectTypes
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data",
                    "Creating JSON structures", 0.4f);
                var stateAndActionJsonStructure = CreateStateAndActionJson(statesInfos, actionInfos);

                // update/write the json
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data",
                    "Writing JSON files", 0.6f);
                WriteJsonFiles(stateAndActionJsonStructure.Item1.ToList(), stateAndActionJsonStructure.Item2.ToList());

                // create 'RegressionGames.zip' in project folder
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data",
                    "Creating .zip file", 0.8f);
                CreateJsonZip();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
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

        private static void GenerateActionClasses(List<RGActionAttributeInfo> actionInfos)
        {
            var fileWriteTasks = new List<(string,Task)>();
            var actionInfosByBehaviour = actionInfos
                .GroupBy(v => (v.BehaviourNamespace, v.BehaviourName, v.BehaviourFileDirectory))
                .ToDictionary(v => v.Key, v => v.ToList());
            foreach (var (behaviourDetails,actionInfoList) in actionInfosByBehaviour)
            {
                var newFileName = $"{behaviourDetails.BehaviourFileDirectory}{Path.DirectorySeparatorChar}Generated_RGActions_{behaviourDetails.BehaviourName}.cs";
                fileWriteTasks.Add((newFileName,
                        GenerateRGActionsClass.Generate(
                            newFileName,
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

        private static IEnumerable<Assembly> GetAssemblies()
        {
            var list = new List<string>();
            var stack = new Stack<Assembly>();

            // Use the Unityeditor assembly as our root     
            // this will get literally everything referenced by this project
            // unity, rg, microsoft, 3rd party, etc
            var exAssembly = Assembly.GetExecutingAssembly();
            stack.Push(exAssembly);
            list.Add(exAssembly.FullName);
            // include the currently loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!list.Contains(assembly.FullName))
                {
                    stack.Push(assembly);
                    list.Add(assembly.FullName);
                }
            }

            do
            {
                var asm = stack.Pop();

                yield return asm;

                foreach (var reference in asm.GetReferencedAssemblies())
                    if (!list.Contains(reference.FullName))
                    {
                        try
                        {
                            stack.Push(Assembly.Load(reference));
                            list.Add(reference.FullName);
                        }
                        catch (Exception)
                        {
                            // somme assemblies can't be loaded (Like mac os code signing)
                        }
                    }

            }
            while (stack.Count > 0);

        }

        private static List<RGActionAttributeInfo> SearchForBotActionAttributes()
        {
            var csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories).ToArray();

            var botActionList = new List<RGActionAttributeInfo>();

            foreach (var csFilePath in csFiles)
            {
                var scriptText = File.ReadAllText(csFilePath);
                
                // optimization... this slightly limits our inheritance cases by assuming the child has [RGAction]
                // but for now its worth the tradeoff in time to avoid compiling all these files
                if (scriptText.Contains("[RGAction"))
                {

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

                        var rgStateTypeAttribute = classDeclaration.AttributeLists.SelectMany(attrList => attrList.Attributes)
                            .FirstOrDefault(attr => attr.Name.ToString() == "RGStateType");
                        
                        var entityTypeName = behaviourName;

                        if (rgStateTypeAttribute != null)
                        {
                            var args = rgStateTypeAttribute.ArgumentList.Arguments;
                            if (args.Count >0 && args[0] is { Expression: LiteralExpressionSyntax literal })
                            {
                                if (bool.TryParse(literal.Token.ValueText, out _))
                                {
                                    // not the type
                                }
                                else
                                {
                                    // is string
                                    entityTypeName = literal.Token.ValueText;
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
                                BehaviourFileDirectory =
                                    csFilePath.Substring(0, csFilePath.LastIndexOf(Path.DirectorySeparatorChar)),
                                // if this wasn't in a sample project folder, we need to generate CS for it
                                ShouldGenerateCSFile = true,
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
            }

            return botActionList;

        }

        private static void CleanupPreviousFilesWithPathAndPattern(string path, string searchPattern)
        {
            // find all .cs files that match our pattern and remove them
            var filesToRemove = Directory.EnumerateFiles(path, searchPattern, SearchOption.AllDirectories);
            
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
            var csFiles = Directory.EnumerateFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

            var fileWriteTasks = new List<(string,Task)>();
            // for each .cs file in the project
            foreach (var csFilePath in csFiles)
            {
                var scriptText = File.ReadAllText(csFilePath);

                // optimization... this slightly limits our inheritance cases by assuming the child has [RGState..]
                // but for now its worth the tradeoff in time to avoid compiling all these files
                if (scriptText.Contains("[RGState"))
                {
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
                        var nameSpace = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>()
                            .FirstOrDefault()?.Name.ToString();
                        var behaviourName = classDeclaration.Identifier.ValueText;

                        var rgStateTypeAttribute = classDeclaration.AttributeLists.SelectMany(attrList => attrList.Attributes)
                            .FirstOrDefault(attr => attr.Name.ToString() == "RGStateType");

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

                            var args = rgStateTypeAttribute.ArgumentList.Arguments;
                            if (args.Count > 0 && args[0] is { Expression: LiteralExpressionSyntax arg0 })
                            {
                                if (bool.TryParse(arg0.Token.ValueText, out isPlayer))
                                {
                                    // not the type, but set our player flag :)
                                }
                                else
                                {
                                    // is string
                                    entityTypeName = arg0.Token.ValueText;
                                }
                            }
                            if (args.Count > 1 && args[1] is { Expression: LiteralExpressionSyntax arg1 })
                            {
                                if (bool.TryParse(arg1.Token.ValueText, out isPlayer))
                                {
                                    // not the type, but set our player flag :)
                                }
                                else
                                {
                                    // is string
                                    entityTypeName = arg1.Token.ValueText;
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
                                var nextEntry = GetStateNameFieldNameAndTypeForMember(true, semanticModel,
                                    behaviourName, member, includeFlags);
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
                                            attr.Name.ToString() == "Obsolete"
                                        )
                                    )
                                );

                            foreach (var member in includeMembers)
                            {
                                var hasRGState = membersWithRGStateAttribute.Contains(member);
                                var nextEntry = GetStateNameFieldNameAndTypeForMember(hasRGState, semanticModel,
                                    behaviourName, member, includeFlags);
                                stateMetadata.Add(nextEntry);
                            }
                        }
                        else
                        {
                            // do nothing.. had no RGState related things
                        }

                        if (stateMetadata.Count > 0)
                        {
                            var fileDirectory =
                                csFilePath.Substring(0, csFilePath.LastIndexOf(Path.DirectorySeparatorChar));
                            var newFileName =
                                $"{fileDirectory}{Path.DirectorySeparatorChar}Generated_RGStateEntity_{behaviourName}.cs";
                            fileWriteTasks.Add((newFileName,
                                GenerateRGStateEntityClass.Generate(newFileName, entityTypeName, isPlayer,
                                    behaviourName, nameSpace, stateMetadata)));
                        }
                    }
                }
            }

            Task.WaitAll(fileWriteTasks.Select(v => v.Item2).ToArray());
            foreach (var (filename, _) in fileWriteTasks)
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
                .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(RGActionRequest)));
            foreach (var rgActionRequestType in rgActionRequestTypes)
            {
                var entityTypeNameField = rgActionRequestType.GetField("EntityTypeName");
                var entityTypeName = entityTypeNameField?.GetValue(null)?.ToString();
                if (string.IsNullOrEmpty(entityTypeName))
                {
                    entityTypeName = null;
                }
                if (entityTypeNameField == null)
                {
                    RecordError($"{rgActionRequestType.FullName} must define field 'public static readonly string EntityTypeName = \"<EntityTypeName>\";' where '<EntityTypeName>' is either the RG State type this action is callable for or is the name of the MonoBehaviour with which this action is related.  If this is defined as null, then the action should be globally usable like ClickButton or KeyPress.");
                }
                
                var actionName = rgActionRequestType.GetField("ActionName")?.GetValue(null)?.ToString();
                if (string.IsNullOrEmpty(actionName))
                {
                    RecordError($"{rgActionRequestType.FullName} must define field 'public static readonly string ActionName = \"<ActionName>\";' where '<ActionName>' is the ActionCommand this RGActionRequest represents.");
                }

                if (_hasExtractProblem)
                {
                    break;
                }
                
                if (!actionInfos.TryGetValue(entityTypeName ?? "NULL", out var theList))
                {
                    theList = new List<RGActionInfo>();
                    actionInfos[entityTypeName ?? "NULL"] = theList;
                }

                var inList = theList.FirstOrDefault(v => v.ActionName == actionName) != null;
                if (!inList)
                {
                    // get the parameters by interrogating the constructor args
                    // this assumes that there is only one named constructor for these custom RGActionRequest classes
                    var allConstructors = rgActionRequestType.GetConstructors();

                    if (allConstructors.Length < 1)
                    {
                        RecordError(
                            $"RGActionRequest class: {rgActionRequestType.FullName} does not define the required single named constructor with arguments");
                        break;
                    }

                    if (allConstructors.Length > 1)
                    {
                        RecordError(
                            $"RGActionRequest class: {rgActionRequestType.FullName} defines multiple named constructors with arguments, but only a single constructor is allowed");
                        break;
                    }

                    var constructorArgs = allConstructors[0].GetParameters();

                    // add an entry for this hand written rgActionRequest
                    theList.Add(new RGActionInfo()
                        {
                            ClassName = rgActionRequestType.FullName,
                            ActionName = actionName,
                            Parameters = constructorArgs.Select(v => new RGParameterInfo()
                            {
                                Name = v.Name,
                                Type = GetTypeString(v.ParameterType, out var isNullable),
                                Nullable = isNullable
                            }).ToList()
                        }
                    );
                }
                else
                {
                    RecordError(
                        $"Multiple RGActionRequest classes specify action: {actionName} on the same entityType: {entityTypeName}");
                    break;
                }
            }

            return actionInfos;
        }

        private static string GetTypeString(Type type, out bool isNullable)
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            isNullable = underlyingType != null;
            type = underlyingType ?? type;
            
            var result = PrimitiveTypeAliases.TryGetValue(type, out var primitiveType) ? primitiveType : GetCSharpRepresentation(type, true);
            return result;
        }
        
        private static string GetCSharpRepresentation( Type type, bool trimArgCount)
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            var isNullable = (underlyingType != null);
            type = underlyingType ?? type;

            if (!type!.IsGenericType)
            {
                return (PrimitiveTypeAliases.TryGetValue(type, out var primitiveType) ? primitiveType : type.Namespace+"."+type.Name) + (isNullable ? "?" : "");
            }
            
            var genericArgs = type.GetGenericArguments().ToList();
            return GetCSharpRepresentation( type, trimArgCount, genericArgs );
        }

        private static string GetCSharpRepresentation( Type type, bool trimArgCount, List<Type> availableArguments )
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            var isNullable = (underlyingType != null);
            type = underlyingType ?? type;

            if (!type!.IsGenericType)
            {
                return (PrimitiveTypeAliases.TryGetValue(type, out var primitiveType) ? primitiveType : type.Namespace+"."+type.Name) + (isNullable ? "?" : "");
            }

            var value = type.Namespace+"."+type.Name;
            if( trimArgCount && value.IndexOf("`") > -1 ) {
                value = value.Substring( 0, value.IndexOf( "`" ) );
            }

            if( type.DeclaringType != null ) {
                // This is a nested type, build the nesting type first
                value = GetCSharpRepresentation( type.DeclaringType, trimArgCount, availableArguments ) + "+" + value;
            }

            // Build the type arguments (if any)
            var argString = "";
            var thisTypeArgs = type.GetGenericArguments();
            for( var i = 0; i < thisTypeArgs.Length && availableArguments.Count > 0; i++ ) {
                if( i != 0 ) argString += ", ";

                argString += GetCSharpRepresentation( availableArguments[0], trimArgCount );
                availableArguments.RemoveAt( 0 );
            }

            // If there are type arguments, add them with < >
            if( argString.Length > 0 ) {
                value += "<" + argString + ">";
            }

            value += (isNullable ? "?" : "");

            return value;
        }
        
        private static readonly Dictionary<Type, string> PrimitiveTypeAliases =
            new()
            {
                { typeof(byte), "byte" },
                { typeof(sbyte), "sbyte" },
                { typeof(short), "short" },
                { typeof(ushort), "ushort" },
                { typeof(int), "int" },
                { typeof(uint), "uint" },
                { typeof(long), "long" },
                { typeof(ulong), "ulong" },
                { typeof(float), "float" },
                { typeof(double), "double" },
                { typeof(decimal), "decimal" },
                { typeof(object), "object" },
                { typeof(bool), "bool" },
                { typeof(char), "char" },
                { typeof(string), "string" },
                { typeof(void), "void" }
            };


        private static List<RGStatesInfo> CreateStateInfoFromRGStateEntities()
        {
            var result = new List<RGStatesInfo>(); 
            var loadedAndReferencedAssemblies = GetAssemblies();
            var rgStateEntityTypes = loadedAndReferencedAssemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IRGStateEntity).IsAssignableFrom(t) || t.IsSubclassOf(typeof(RGStateEntityBase)))
                .Where(t => !t.IsAbstract && !t.IsInterface &&t != typeof(RGStateEntity_Empty) && t != typeof(RGStateEntity_Core) && !t.IsSubclassOf(typeof(RGStateEntity_Core)));

            foreach (var rgStateEntityType in rgStateEntityTypes)
            {
                var entityTypeNameField = rgStateEntityType.GetField("EntityTypeName");
                var entityTypeName = entityTypeNameField?.GetValue(null)?.ToString();
                if (string.IsNullOrEmpty(entityTypeName))
                {
                    entityTypeName = null;
                }
                if (entityTypeNameField == null)
                {
                    RecordError($"{rgStateEntityType.FullName} must define field 'public static readonly string EntityTypeName = \"<EntityTypeName>\";' where '<EntityTypeName>' is either the RG State type this action is callable for or is the name of the MonoBehaviour with which this action is related.  If this is defined as null, then the action should be globally usable like ClickButton or KeyPress.");
                }
                // all RGStateEntity accessors are public properties (=> impls).. don't get properties from classes in the heirarchy
                var className = rgStateEntityType.FullName;
                var properties = rgStateEntityType.GetMembers()
                    .Where(v => v.MemberType == MemberTypes.Property && typeof(IRGStateEntity).IsAssignableFrom(v.DeclaringType));

                var stateList = new List<RGStateInfo>();
                foreach (var memberInfo in properties)
                {
                     var propertyType = ((PropertyInfo)memberInfo).PropertyType;
                     

                    stateList.Add(new RGStateInfo
                    {
                        StateName = memberInfo.Name,
                        Type = GetTypeString(propertyType, out _)
                    });
                }

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
        
        private static (List<RGEntityStatesJson>, List<RGEntityActionsJson>) CreateStateAndActionJson(
            List<RGStatesInfo> statesInfos, Dictionary<string, List<RGActionInfo>> actionInfos)
        {

            var statesJson = statesInfos.Select(v => new RGEntityStatesJson()
            {
                ObjectType = v.EntityTypeName,
                States = v.States.ToHashSet()
            }).ToList();

            var actionsJson = actionInfos.Select(v => new RGEntityActionsJson()
            {
                // handle no type keys
                ObjectType = v.Key == "NULL" ? null : v.Key,
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
