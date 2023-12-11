using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.RGBotConfigs;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
#endif

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

    public class RGCodeGenerator
    {
#if UNITY_EDITOR
        // Used to exclude sample projects' directories from generation
        // so that we don't duplicate .cs files that are already included in the sample projects.
        // But while still scanning them so that we can include their States/Actions in the json.
        private static readonly HashSet<string> ExcludeDirectories = new() {
           "ThirdPersonDemoURP"
        };

        private static bool _hasExtractProblem = false;

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
                    string zipPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "RegressionGames.zip");
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
            // remove previous RGActions
            string dataPath = Application.dataPath;
            string directoryToDelete = Path.Combine(dataPath, "RegressionGames/Runtime/GeneratedScripts/RGActions").Replace("\\", "/");

            if (Directory.Exists(directoryToDelete))
            {
                Directory.Delete(directoryToDelete, true);
                File.Delete(directoryToDelete + ".meta");
            }
            GenerateRGSerializationClass.Generate(actionInfos);
            GenerateRGActionClasses.Generate(actionInfos);
            GenerateRGActionMapClass.Generate(actionInfos);
        }

        private static void ExtractGameContextHelper()
        {
            try
            {

                // just in case they haven't done this recently or ever...
                // find and extract RGState data
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Searching for RGState attributes", 0.1f);
                var stateAttributesInfos = SearchForBotStateAttributes();
                // generate classes so that their RGStateEntity classes exist before the CreateStateInfoFromRGStateEntities step
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Generating classes for RGState attributes", 0.2f);
                GenerateStateClasses(stateAttributesInfos);

                // find and extract RGAction data
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Searching for RGAction attributes", 0.3f);
                var actionAttributeInfos = SearchForBotActionAttributes();
                // generate classes
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Generating classes for RGAction attributes", 0.4f);
                GenerateActionClasses(actionAttributeInfos);

                if (_hasExtractProblem)
                {
                    // if the code generation phase failed.. don't waste any more time
                    return;
                }

                // Find RGStateEntity scripts and generate state info from them
                // Do NOT include the previous state infos.. so we don't have dupes
                // This gives us a consistent view across both generated and hand written state class entities
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Extracting state info rom RGStateEntity classes", 0.5f);
                var statesInfos = CreateStateInfoFromRGStateEntities();

                var actionInfos = actionAttributeInfos.Select(v => v.toRGActionInfo()).ToList();

                // add global click button action
                actionInfos.Add(new RGActionInfo()
                {
                    ActionClassName = typeof(RGAction_ClickButton).FullName,
                    ActionName = "ClickButton",
                    Parameters = new List<RGParameterInfo>()
                });

                /* TODO (REG-1476): Solve how to add hand written actions automatically
                   this will help us avoid weird assembly references also.

                // add key press action
                actionInfos.Add(new RGActionInfo()
                {
                    ActionClassName = typeof(RGAction_KeyPress).FullName,
                    ActionName = "KeyPress",
                    Parameters = new List<RGParameterInfo>()
                    {
                        new ()
                        {
                            Name = "keyId",
                            Type = "string",
                            Nullable = false
                        },
                        new ()
                        {
                            Name = "holdTime",
                            Type = "double",
                            Nullable = true
                        }
                    }
                });*/

                // if these have been associated to gameObjects with RGEntities, fill in their objectTypes
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Populating Object types", 0.6f);
                var stateAndActionJsonStructure = CreateStateAndActionJsonWithObjectTypes(statesInfos, actionInfos);

                // update/write the json
                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", "Writing JSON files", 0.8f);
                WriteJsonFiles(stateAndActionJsonStructure.Item1.ToList(), stateAndActionJsonStructure.Item2.ToList());

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
                ExcludeDirectories.Select(ed => Path.Combine(UnityEngine.Device.Application.dataPath, ed)).ToArray();

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

                var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var classDeclaration in classDeclarations)
                {
                    var className = classDeclaration.Identifier.ValueText;
                    var nameSpace = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()
                        ?.Name.ToString();
                    var botActionMethods = classDeclaration.Members
                        .OfType<MethodDeclarationSyntax>()
                        .Where(method =>
                            method.AttributeLists.Any(attrList =>
                                attrList.Attributes.Any(attr =>
                                    attr.Name.ToString() == "RGAction")))
                        .ToList();
                    var isPartial = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
                    if (botActionMethods.Count > 0 && !isPartial)
                    {
                        RecordError(
                            $"Error: Class '{className}' must be marked with the 'partial' keyword (for example 'public partial class {className}') to use the [RGAction] attribute.");
                        continue;
                    }

                    foreach (var method in botActionMethods)
                    {
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
            }

            GenerateRGStateClasses.Generate(rgStateAttributesInfos);
        }

        private static List<RGStateAttributesInfo> SearchForBotStateAttributes()
        {
            // make sure to exclude any sample project directories from the search
            var excludedPaths =
                ExcludeDirectories.Select(ed => Path.Combine(UnityEngine.Device.Application.dataPath, ed)).ToArray();

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
                        .Where(m => m.AttributeLists.Any(a => a.Attributes.Any(attr => attr.Name.ToString() == "RGState")))
                        .ToList();
                    var isPartial = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
                    if (membersWithRGState.Count > 0 && !isPartial)
                    {
                        // The class isn't partial
                        RecordError($"Error: Class '{className}' must be marked with the 'partial' keyword (for example 'public partial class {className}') to use the [RGState] attribute.");
                        continue;
                    }

                    foreach (var member in membersWithRGState)
                    {
                        bool hasError = false;

                        if (member is FieldDeclarationSyntax fieldDeclaration)
                        {
                            if (!fieldDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                            {
                                RecordError($"Error: Field '{fieldDeclaration.Declaration.Variables.First().Identifier.ValueText}' in class '{className}' is not public.");
                                hasError = true;
                            }
                        }
                        else if (member is MethodDeclarationSyntax methodDeclaration)
                        {
                            if (!methodDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                            {
                                RecordError($"Error: Method '{methodDeclaration.Identifier.ValueText}' in class '{className}' is not public.");
                                hasError = true;
                            }
                            else if (methodDeclaration.ParameterList.Parameters.Count > 0)
                            {
                                RecordError($"Error: Method '{methodDeclaration.Identifier.ValueText}' in class '{className}' has parameters, which is not allowed.");
                                hasError = true;
                            }
                            else if (methodDeclaration.ReturnType is PredefinedTypeSyntax predefinedType && predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword))
                            {
                                RecordError($"Error: Method '{methodDeclaration.Identifier.ValueText}' in class '{className}' has a void return type, which is not allowed.");
                                hasError = true;
                            }
                        }
                        else if (member is DelegateDeclarationSyntax delegateDeclaration)
                        {
                            if (!delegateDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                            {
                                RecordError($"Error: Delegate '{delegateDeclaration.Identifier.ValueText}' in class '{className}' is not public.");
                                hasError = true;
                            }
                            else if (delegateDeclaration.ParameterList.Parameters.Count > 0)
                            {
                                RecordError($"Error: Delegate '{delegateDeclaration.Identifier.ValueText}' in class '{className}' has parameters, which is not allowed.");
                                hasError = true;
                            }
                            else if (delegateDeclaration.ReturnType is PredefinedTypeSyntax predefinedType && predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword))
                            {
                                RecordError($"Error: Delegate '{delegateDeclaration.Identifier.ValueText}' in class '{className}' has a void return type, which is not allowed.");
                                hasError = true;
                            }
                        }
                        else if (member is PropertyDeclarationSyntax propertyDeclaration)
                        {
                            if (!propertyDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                            {
                                RecordError(
                                    $"Error: Property '{propertyDeclaration.Identifier.ValueText}' in class '{className}' is not public.");
                                hasError = true;
                            }
                        }

                        if (hasError)
                        {
                            continue;
                        }

                        string fieldType = member is MethodDeclarationSyntax or DelegateDeclarationSyntax ? "method" : "variable";

                        string fieldName = null;
                        string type = null;
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
                            case DelegateDeclarationSyntax del:
                                fieldName = del.Identifier.ValueText;
                                type = RemoveGlobalPrefix(semanticModel
                                    .GetTypeInfo(del.ReturnType)
                                    .Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                                break;
                            case MethodDeclarationSyntax method:
                                fieldName = method.Identifier.ValueText;
                                type = RemoveGlobalPrefix(semanticModel
                                    .GetTypeInfo(method.ReturnType)
                                    .Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                                break;
                            default:
                                RecordError(
                                    $"Error: [RGState] attribute in class '{className}' is applied to an invalid declaration: {member}.");
                                continue;
                        }

                        string stateName = fieldName;
                        var attribute = member.AttributeLists.SelectMany(attrList => attrList.Attributes)
                            .FirstOrDefault(attr => attr.Name.ToString() == "RGState");

                        var attributeArgument = attribute?.ArgumentList?.Arguments.FirstOrDefault();
                        if (attributeArgument is { Expression: LiteralExpressionSyntax literal })
                        {
                            stateName = literal.Token.ValueText;
                        }

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
            var csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories).ToHashSet();

            // look through all packages that are part of this project
            var listRequest = Client.List(true, false);

            while (!listRequest.IsCompleted)
            {
                Thread.Sleep(100);
            }

            // add the package files to the search
            var packageCollection = listRequest.Result;
            foreach (var packageInfo in packageCollection)
            {
                var packagePath = packageInfo.resolvedPath;
                var packageFiles = Directory.GetFiles(packagePath, "*.cs", SearchOption.AllDirectories);

                EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", $"Extracting state info from package {packageInfo.displayName}", 0.53f);
                foreach (var packageFile in packageFiles)
                {
                    csFiles.Add(packageFile);
                }
            }

            List<RGStatesInfo> rgStateInfoList = new List<RGStatesInfo>();

            foreach (string csFilePath in csFiles)
            {
                string scriptText = File.ReadAllText(csFilePath);
                // optimization because compiling 100s or 1000s of cs files to check type hierarchies takes too long
                if (scriptText.Contains("RGStateEntity"))
                {
                    SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(scriptText);

                    var compilation = CSharpCompilation.Create("RGCompilation")
                        .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                        .AddSyntaxTrees(syntaxTree);

                    CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
                    var semanticModel = compilation.GetSemanticModel(syntaxTree);

                    var rgStateEntityClassDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                        .Where(cd => semanticModel.GetDeclaredSymbol(cd).BaseType.Name == "RGStateEntity");

                    EditorUtility.DisplayProgressBar("Extracting Regression Games Agent Builder Data", $"Extracting state info from RGStateEntity classes in file {csFilePath}", 0.57f);
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
                            string fieldName;
                            string type;
                            switch (member)
                            {
                                case FieldDeclarationSyntax fieldDeclaration:
                                {
                                    fieldName = fieldDeclaration.Declaration.Variables.First().Identifier.ValueText;
                                    type = RemoveGlobalPrefix(semanticModel
                                        .GetTypeInfo(fieldDeclaration.Declaration.Type)
                                        .Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                                    if (!fieldDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                                    {
                                        RecordWarning($"Field '{fieldDeclaration.Declaration.Variables.First().Identifier.ValueText}' in class '{className}' is not public and will not be included in the available state fields.");
                                        continue;
                                    }

                                    break;
                                }
                                case PropertyDeclarationSyntax propertyDeclaration:
                                {
                                    fieldName = propertyDeclaration.Identifier.ValueText;
                                    type = RemoveGlobalPrefix(semanticModel
                                        .GetTypeInfo(propertyDeclaration.Type)
                                        .Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                                    if (!propertyDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                                    {
                                        RecordWarning($"Property '{propertyDeclaration.Identifier.ValueText}' in class '{className}' is not public and will not be included in the available state properties.");
                                        continue;
                                    }

                                    break;
                                }
                                default:
                                    // no methods
                                    continue;
                            }

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
                                States = stateList
                            });
                        }
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

        /**
         * WARNING/NOTE: This should be used after checking for changes in the editor or other prompting
         * to prevent users from losing their unsaved work.
         */
        private static (List<RGEntityStatesJson>, List<RGEntityActionsJson>) CreateStateAndActionJsonWithObjectTypes(
            List<RGStatesInfo> statesInfos, List<RGActionInfo> actionInfos)
        {

            // map of object names and object types
            (HashSet<RGEntityStatesJson>, HashSet<RGEntityActionsJson>) result = new()
            {
                Item1 = new HashSet<RGEntityStatesJson>(),
                Item2 = new HashSet<RGEntityActionsJson>()
            };

            // iterate through all scenes rather than only the current ones in the editor
            var startingActiveScenePath = SceneManager.GetActiveScene().path;
            List<string> allActiveScenePaths = new();
            HashSet<string> allLoadedScenePaths = new();
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

            // Get for all the scenes in the build
            EditorBuildSettingsScene[] scenesInBuild = EditorBuildSettings.scenes;
            foreach (var editorScene in scenesInBuild)
            {
                // include currently enabled scenes for the build
                if (editorScene.enabled)
                {
                    // Open the scene
                    EditorSceneManager.OpenScene(editorScene.path, OpenSceneMode.Single);

                    // For objects in the scene.. we let this re-process duplicate objectTypes to make sure there isn't any inconsistency between game objects of the same ObjectType
                    var allEntities = Object.FindObjectsOfType<RGEntity>().Where(v => !string.IsNullOrEmpty(v.objectType));
                    foreach (var entity in allEntities)
                    {
                        var (stateClassNames,actionClassNames) = entity.LookupStatesAndActions();

                        var entityStateActionJson = DeriveStateAndActionJsonForEntity(entity.objectType, stateClassNames, actionClassNames, statesInfos, actionInfos);

                        CheckForMisMatchedStateOrActionsOnEntity(entity, entityStateActionJson, result);
                        result.Item1.Add(entityStateActionJson.Item1);
                        if (entityStateActionJson.Item2.Actions.Count > 0)
                        {
                            result.Item2.Add(entityStateActionJson.Item2);
                        }
                    }

                }
            }

            var firstReloadScene = true;
            // get the editor back to the scenes they had open before we started
            Scene? goBackToStartingActiveScene = null;

            foreach (var activeScenePath in allActiveScenePaths)
            {
                // open the first in singular to clear editor, then rest additive
                var mode = firstReloadScene
                    ? OpenSceneMode.Single
                    : (allLoadedScenePaths.Contains(activeScenePath)
                        ? OpenSceneMode.Additive
                        : OpenSceneMode.AdditiveWithoutLoading);
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

                // since we only load and don't instantiate an instance of this prefab
                // we don't need to manage destroying it
                if (prefab != null)
                {
                    RGEntity prefabComponent = prefab.GetComponent<RGEntity>();
                    if (prefabComponent != null && !string.IsNullOrEmpty(prefabComponent.objectType))
                    {
                        var (stateClassNames,actionClassNames) = prefabComponent.LookupStatesAndActions();

                        var prefabStateActionJson = DeriveStateAndActionJsonForEntity(prefabComponent.objectType, stateClassNames, actionClassNames, statesInfos, actionInfos);
                        CheckForMisMatchedStateOrActionsOnEntity(prefabComponent, prefabStateActionJson, result);

                        result.Item1.Add(prefabStateActionJson.Item1);
                        if (prefabStateActionJson.Item2.Actions.Count > 0)
                        {
                            result.Item2.Add(prefabStateActionJson.Item2);
                        }
                    }
                }
            }

            (List<RGEntityStatesJson>, List<RGEntityActionsJson>) listResult = new  ()
            {
                Item1 = result.Item1.ToList(),
                Item2 = result.Item2.ToList()
            };

            listResult.Item1.Sort((a,b) => a.ObjectType.CompareTo(b.ObjectType));
            listResult.Item2.Sort((a,b) => a.ObjectType.CompareTo(b.ObjectType));
            return listResult;
        }

        private static void CheckForMisMatchedStateOrActionsOnEntity(RGEntity entity, (RGEntityStatesJson, RGEntityActionsJson) entityStateActionJson, (HashSet<RGEntityStatesJson>, HashSet<RGEntityActionsJson>)result)
        {
            if(result.Item1.TryGetValue(entityStateActionJson.Item1, out var existingItem1))
            {
                // this is a bit expensive, but necessary to ensure all RGStateEntities of the same ObjectType expose the same state/Actions
                if (existingItem1.States.Count != entityStateActionJson.Item1.States.Count ||
                    !entityStateActionJson.Item1.States.ToList().TrueForAll(newVal =>
                    {
                        if (existingItem1.States.TryGetValue(newVal, out var existingVal))
                        {
                            if (existingVal.Type != newVal.Type)
                            {
                                return false;
                            }
                        }
                        return true;
                    }))
                {
                    RecordWarning($"RGEntity of ObjectType: {entity.objectType} has conflicting state definitions on different game objects or prefabs;  state lists: [{string.Join(", ", entityStateActionJson.Item1.States)}] <-> [{string.Join(", ", existingItem1.States)}]");
                }
            }
            if(result.Item2.TryGetValue(entityStateActionJson.Item2, out var existingItem2))
            {
                // this is a bit expensive, but necessary to ensure all RGStateEntities of the same ObjectType expose the same state/Actions
                if (existingItem2.Actions.Count != entityStateActionJson.Item2.Actions.Count ||
                    !entityStateActionJson.Item2.Actions.ToList().TrueForAll(newVal =>
                    {
                        if (existingItem2.Actions.TryGetValue(newVal, out var existingVal))
                        {
                            if (existingVal.Parameters.Count != newVal.Parameters.Count ||
                                !newVal.Parameters.TrueForAll(newParam =>
                                {
                                    var foundParam = existingVal.Parameters.FirstOrDefault(t => t.Name == newParam.Name);
                                    if (newParam.Type != foundParam?.Type)
                                    {
                                        return false;
                                    }
                                    return true;
                                }))
                            {
                                return false;
                            }
                        }
                        return true;
                    }))
                {
                    RecordWarning($"RGEntity of ObjectType: {entity.objectType} has conflicting action definitions on different game objects or prefabs;  action lists: [{string.Join(", ", entityStateActionJson.Item2.Actions)}] <-> [{string.Join(", ", existingItem2.Actions)}]");
                }
            }
        }

        private static (RGEntityStatesJson, RGEntityActionsJson) DeriveStateAndActionJsonForEntity(string objectType, HashSet<string> stateClassNames, HashSet<string> actionClassNames, List<RGStatesInfo> statesInfos, List<RGActionInfo> actionInfos)
        {
            (RGEntityStatesJson, RGEntityActionsJson) result = new();

            var states = new HashSet<RGStateInfo>();
            foreach (var stateClassName in stateClassNames)
            {
                // handle States
                var stateInfo = statesInfos.FirstOrDefault(v => v.ClassName == stateClassName);
                if (stateInfo != null)
                {
                    foreach (var stateInfoState in stateInfo.States)
                    {
                        if (states.TryGetValue(stateInfoState, out var existingState))
                        {
                            if (stateInfoState.Type != existingState.Type)
                            {
                                RecordWarning($"RGEntity of ObjectType: {objectType} has multiple definitions of state: {existingState.StateName} with conflicting types: {existingState.Type} <-> {stateInfoState.Type}");
                            }
                        }
                        states.Add(stateInfoState);
                    }
                }
                else
                {
                    RecordError($"Information not found for State: {stateClassName} on RGEntity with ObjectType: {objectType}.  Please contact Regression Games for support with this issue.");
                }

            }
            var entityStateJson = new RGEntityStatesJson()
            {
                ObjectType = objectType,
                States = states
            };
            result.Item1 = entityStateJson;

            // handle Actions
            var actions = new HashSet<RGActionInfo>();
            foreach (var actionClassName in actionClassNames)
            {
                var actionInfo = actionInfos.FirstOrDefault(v => v.ActionClassName == actionClassName);
                if (actionInfo != null)
                {
                    if (actions.TryGetValue(actionInfo, out var existingAction))
                    {
                        if (actionInfo.Parameters.Count != existingAction.Parameters.Count &&
                            !actionInfo.Parameters.TrueForAll(v => existingAction.Parameters.Contains(v)))
                        {
                            RecordWarning($"RGEntity of ObjectType: {objectType} has multiple definitions of action: {existingAction.ActionName} with conflicting parameter lists: [{string.Join(", ", existingAction.Parameters)}] <-> [{string.Join(", ", actionInfo.Parameters)}]");
                        }
                    }

                    actions.Add(actionInfo);
                }
                else
                {
                    RecordError($"Information not found for Action: {actionClassName} on RGEntity with ObjectType: {objectType}.  Please contact Regression Games for support with this issue.");
                }

            }
            var entityActionJson = new RGEntityActionsJson()
            {
                ObjectType = objectType,
                Actions = actions
            };
            result.Item2 = entityActionJson;

            return result;
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
            string folderPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "RegressionGamesZipTemp");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, $"{fileName}.json");
            File.WriteAllText(filePath, json);
        }
#endif
    }

}
