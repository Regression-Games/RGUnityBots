#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.Compilation;
using UnityEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RegressionGames.ActionManager.Actions;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Assembly = UnityEditor.Compilation.Assembly;
using Button = UnityEngine.UI.Button;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RegressionGames.ActionManager
{
    /// <summary>
    /// Class used to store any warnings that occur during the analysis.
    /// </summary>
    public class RGActionAnalysisWarning
    {
        private readonly string _filePath;
        private readonly int _startLineNumber;
        private readonly int _endLineNumber;
        private readonly string _message;

        public RGActionAnalysisWarning(string message, SyntaxNode node = null)
        {
            if (node != null)
            {
                _filePath = node.SyntaxTree.FilePath;
                var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
                _startLineNumber = lineSpan.StartLinePosition.Line;
                _endLineNumber = lineSpan.EndLinePosition.Line;
            }
            _message = message;
        }

        public override string ToString()
        {
            if (_filePath != null)
            {
                return $"{_filePath}:{_startLineNumber}:{_endLineNumber}: {_message}";
            }
            else
            {
                return _message;
            }
        }
    }

    public class RGActionAnalysis : CSharpSyntaxWalker
    {
        // actions that were identified in a method outside of a MonoBehaviour (mapping from method name -> action path -> action)
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, RGGameAction>> _unboundActions = new();

        // Mapping from action path to action. Populated as the analysis proceeds.
        // This is considered a "raw" set of actions, in that it is possible that redundant/equivalent
        // actions are generated. The final output of the analysis combines any equivalent actions.
        private readonly ConcurrentDictionary<string, RGGameAction> _rawActions = new();

        // Mapping that associates syntax nodes with the actions that were identified at those points.
        private readonly ConcurrentDictionary<SyntaxNode, List<RGGameAction>> _rawActionsByNode = new();

        private readonly ThreadLocal<SemanticModel> _currentModel = new();
        private readonly ThreadLocal<SyntaxTree> _currentTree= new();
        private readonly ThreadLocal<Dictionary<AssignmentExpressionSyntax, DataFlowAnalysis>> _assignmentExprs = new(() => new());
        private readonly ThreadLocal<Dictionary<LocalDeclarationStatementSyntax, DataFlowAnalysis>> _localDeclarationStmts = new(() => new());

        private readonly ISet<RGGameAction> _actions = new HashSet<RGGameAction>();
        private readonly List<RGActionAnalysisWarning> _warnings = new();

        private readonly Dictionary<Type, FieldInfo[]> _fieldInfoCache = new();
        private readonly Dictionary<Type, PropertyInfo[]> _propertyInfoCache = new();

        private readonly bool _displayProgressBar;

        private bool _unboundActionsNeedResolution;

        public RGActionAnalysis(bool displayProgressBar = false)
        {
            _displayProgressBar = displayProgressBar;
        }

        /// <summary>
        /// Returns the set of assembly names that should be ignored by the analysis
        /// </summary>
        private ISet<string> GetIgnoredAssemblyNames()
        {
            var rgAssembly = FindRGPlayerAssembly();
            if (rgAssembly == null)
            {
                return null;
            }

            var rgEditorAssembly = FindRGEditorAssembly();
            if (rgEditorAssembly == null)
            {
                return null;
            }

            // ignore RG SDK assemblies and their dependencies
            HashSet<string> result = new()
            {
                Path.GetFileNameWithoutExtension(rgAssembly.outputPath),
                Path.GetFileNameWithoutExtension(rgEditorAssembly.outputPath)
            };
            foreach (var asmPath in rgAssembly.allReferences)
            {
                result.Add(Path.GetFileNameWithoutExtension(asmPath));
            }
            foreach (var asmPath in rgEditorAssembly.allReferences)
            {
                result.Add(Path.GetFileNameWithoutExtension(asmPath));
            }

            return result;
        }

        private static Assembly FindRGPlayerAssembly()
        {
            var rgAsmName = Path.GetFileName(typeof(BotSegmentsPlaybackController).Assembly.Location);
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            foreach (var assembly in assemblies)
            {
                if (Path.GetFileName(assembly.outputPath) == rgAsmName)
                {
                    return assembly;
                }
            }
            return null;
        }

        private static Assembly FindRGEditorAssembly()
        {
            var rgEditorAsmName = Path.GetFileName(typeof(RGActionAnalysis).Assembly.Location);
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            foreach (var assembly in assemblies)
            {
                if (Path.GetFileName(assembly.outputPath) == rgEditorAsmName)
                {
                    return assembly;
                }
            }
            return null;
        }

        /// <summary>
        /// Determines the set of assemblies that should be analyzed.
        /// </summary>
        private IEnumerable<Assembly> GetTargetAssemblies()
        {
            var ignoredAssemblyNames = GetIgnoredAssemblyNames();
            var playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            foreach (var playerAsm in playerAssemblies)
            {
                var playerAsmName = Path.GetFileNameWithoutExtension(playerAsm.outputPath);
                if (ignoredAssemblyNames.Contains(playerAsmName)
                    || playerAsm.sourceFiles.Length == 0)
                {
                    continue;
                }
                var shouldSkip = false;
                foreach (var sourceFile in playerAsm.sourceFiles)
                {
                    if (sourceFile.StartsWith("Packages/"))
                    {
                        shouldSkip = true;
                        break;
                    }
                }
                if (shouldSkip)
                {
                    continue;
                }

                yield return playerAsm;
            }
        }

        /// <summary>
        /// Produces a Roslyn Compilation object for the given assembly, accounting for
        /// all source files, assembly references, and preprocessor directives defined for the assembly.
        /// </summary>
        private Compilation GetCompilationForAssembly(Assembly asm)
        {
            var references = new List<MetadataReference>();
            foreach (var playerAsmRef in asm.allReferences)
            {
                references.Add(MetadataReference.CreateFromFile(playerAsmRef));
            }

            var parseOptions = new CSharpParseOptions().WithPreprocessorSymbols(asm.defines);

            var syntaxTrees = new List<SyntaxTree>();
            foreach (var sourceFile in asm.sourceFiles)
            {
                using var sr = new StreamReader(sourceFile);
                var tree = CSharpSyntaxTree.ParseText(sr.ReadToEnd(), parseOptions, path: sourceFile);
                syntaxTrees.Add(tree);
                sr.Close();
            }

            return CSharpCompilation.Create(asm.name).AddReferences(references).AddSyntaxTrees(syntaxTrees);
        }

        /// <summary>
        /// For the given local variable symbol, this searches for all assignment expressions that
        /// could have possibly assigned a value to the local variable. The set of possible values that
        /// were assigned are returned.
        /// </summary>
        private IEnumerable<ExpressionSyntax> FindCandidateValuesForLocalVariable(ILocalSymbol localSym)
        {
            if (_localDeclarationStmts.Value.Count == 0)
            {
                var root = _currentTree.Value.GetRoot();
                _assignmentExprs.Value.Clear();
                foreach (var assignExpr in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    _assignmentExprs.Value.Add(assignExpr, _currentModel.Value.AnalyzeDataFlow(assignExpr));
                }

                _localDeclarationStmts.Value.Clear();
                foreach (var declExpr in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
                {
                    _localDeclarationStmts.Value.Add(declExpr, _currentModel.Value.AnalyzeDataFlow(declExpr));
                }
            }
            foreach (var entry in _assignmentExprs.Value)
            {
                if (entry.Value.WrittenInside.Any(v => v.Equals(localSym)))
                {
                    var assignExpr = entry.Key;
                    yield return assignExpr.Right;
                }
            }

            foreach (var entry in _localDeclarationStmts.Value)
            {
                if (entry.Value.WrittenInside.Any(v => v.Equals(localSym)))
                {
                    var declStmt = entry.Key;
                    foreach (var varDecl in declStmt.Declaration.Variables)
                    {
                        if (varDecl.Identifier.Value is string varName)
                        {
                            if (varName == localSym.Name)
                            {
                                yield return varDecl.Initializer.Value;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Determines any user input values that the given expression could be derived from.
        /// For example, suppose we have the following code:
        ///  float x = Input.GetAxis("Horizontal")
        ///  float y = x + 2.0f;
        ///  Debug.Log(y)
        ///
        /// This method will return the Input.GetAxis("Horizontal") expression.
        /// If the value is not derived from user input, then nothing is returned.
        /// </summary>
        private IEnumerable<SyntaxNode> FindDerivedUserInput(ExpressionSyntax expr)
        {
            const int maxDepth = 10;
            ISet<SyntaxNode> visited = new HashSet<SyntaxNode>();
            IEnumerable<SyntaxNode> Search(SyntaxNode node, int depth)
            {
                if (depth <= maxDepth && visited.Add(node))
                {
                    var sym = _currentModel.Value.GetSymbolInfo(node).Symbol;
                    if (sym != null)
                    {
                        if (sym is ILocalSymbol localSym)
                        {
                            foreach (var val in FindCandidateValuesForLocalVariable(localSym))
                            {
                                foreach (var res in Search(val, depth + 1))
                                {
                                    yield return res;
                                }
                            }
                        }
                        else
                        {
                            var type = FindType(sym.ContainingType);
                            if (type == typeof(Input) || typeof(InputControl).IsAssignableFrom(type))
                            {
                                yield return node;
                                yield break; // no need to examine children
                            }
                        }
                    }
                    foreach (var childNode in node.ChildNodes())
                    {
                        foreach (var res in Search(childNode, depth + 1))
                        {
                            yield return res;
                        }
                    }
                }
            }
            foreach (var res in Search(expr, 0))
            {
                yield return res;
            }
        }

        /// <summary>
        /// Find a reflection System.Type for the given type symbol.
        /// We can do this because the editor runtime has all the assemblies under analysis already loaded.
        /// </summary>
        private Type FindType(ITypeSymbol typeSymbol)
        {
            if (typeSymbol == null)
            {
                return null;
            }
            var assembly = typeSymbol.ContainingAssembly;
            var fullyQualName = typeSymbol + ", " + assembly;
            return Type.GetType(fullyQualName);
        }

        /// <summary>
        /// This generates a function to evaluate a sequence of dynamic field or property accesses at run time.
        /// For example, if we have an input such as Input.GetKey(mGameSettings.keySettings.leftKey), this will
        /// evaluate the sequence of fields needed to obtain leftKey.
        /// </summary>
        private bool TryGetMemberAccessFunc<T>(ExpressionSyntax memberAccessExpr, out RGActionParamFunc<T> memberAccessFunc)
        {
            var members = new List<MemberInfo>();
            var currentExpr = memberAccessExpr;
            for (;;)
            {
                var symbol = _currentModel.Value.GetSymbolInfo(currentExpr).Symbol;
                if (symbol != null)
                {
                    MemberInfo member = null;
                    if (symbol is IFieldSymbol fieldSym)
                    {
                        var type = FindType(fieldSym.ContainingType);
                        if (type == null)
                        {
                            memberAccessFunc = null;
                            return false;
                        }
                        member = type.GetField(fieldSym.Name,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    }
                    else if (symbol is IPropertySymbol propSym)
                    {
                        var type = FindType(propSym.ContainingType);
                        if (type == null)
                        {
                            memberAccessFunc = null;
                            return false;
                        }
                        member = type.GetProperty(propSym.Name,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    }
                    if (member == null)
                    {
                        memberAccessFunc = null;
                        return false;
                    }
                    members.Add(member);
                }
                else
                {
                    memberAccessFunc = null;
                    return false;
                }

                if (currentExpr is MemberAccessExpressionSyntax { Expression: not ThisExpressionSyntax } memberExpr)
                {
                    currentExpr = memberExpr.Expression;
                }
                else
                {
                    break;
                }
            }
            members.Reverse();

            // if the first field is not static, then its declaring type should match the class declaration
            if (members[0] is FieldInfo { IsStatic: false } memberO)
            {
                var containingClass = memberAccessExpr.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (containingClass == null || memberO.DeclaringType?.Name != containingClass.Identifier.Text)
                {
                    memberAccessFunc = null;
                    return false;
                }
            }

            memberAccessFunc = RGActionParamFunc<T>.MemberAccesses(members);
            return true;
        }

        /// <summary>
        /// Identify all candidate key codes that could be represented by the given expression.
        /// </summary>
        private IEnumerable<RGActionParamFunc<object>> FindCandidateLegacyKeyFuncs(ExpressionSyntax keyExpr)
        {
            bool TryMatch(ExpressionSyntax expr, out RGActionParamFunc<object> keyFunc)
            {
                var symbol = _currentModel.Value.GetSymbolInfo(expr).Symbol;
                if (symbol != null)
                {
                    if (symbol is IFieldSymbol fieldSym)
                    {
                        if (fieldSym.IsStatic && fieldSym.ContainingType?.TypeKind == TypeKind.Enum && fieldSym.ContainingType?.ToString() == "UnityEngine.KeyCode")
                        {
                            // constant keycode (e.g. KeyCode.Space)
                            var keyCode = Enum.Parse<KeyCode>(fieldSym.Name);
                            keyFunc = RGActionParamFunc<object>.Constant(keyCode);
                            return true;
                        }
                        else
                        {
                            // keycode or name is stored in dynamic field
                            if (TryGetMemberAccessFunc(expr, out keyFunc))
                            {
                                return true;
                            }
                        }
                    }
                    else if (symbol is IPropertySymbol && expr is MemberAccessExpressionSyntax memberAccessExpr)
                    {
                        // keycode is stored in a dynamic property
                        if (TryGetMemberAccessFunc(memberAccessExpr, out keyFunc))
                        {
                            return true;
                        }
                    }
                }
                else if (expr is LiteralExpressionSyntax literalExpr)
                {
                    var literalKind = literalExpr.Kind();
                    if (literalKind == SyntaxKind.StringLiteralExpression)
                    {
                        keyFunc = RGActionParamFunc<object>.Constant(literalExpr.Token.ValueText);
                        return true;
                    }
                }

                keyFunc = null;
                return false;
            }

            var matched = false;
            var keySym = _currentModel.Value.GetSymbolInfo(keyExpr).Symbol;
            if (keySym != null)
            {
                if (keySym is ILocalSymbol localSym)
                {
                    // key expression refers to a local variable
                    // check all candidate assignments to the local variable
                    foreach (var valueExpr in FindCandidateValuesForLocalVariable(localSym))
                    {
                        if (TryMatch(valueExpr, out var keyFunc))
                        {
                            matched = true;
                            yield return keyFunc;
                        }
                    }
                }
                else
                {
                    matched = TryMatch(keyExpr, out var keyFunc);
                    if (keyFunc != null)
                    {
                        yield return keyFunc;
                    }
                }
            }
            else
            {
                matched = TryMatch(keyExpr, out var keyFunc);
                if (keyFunc != null)
                {
                    yield return keyFunc;
                }
            }
            if (!matched)
            {
                AddAnalysisWarning("Could not identify key code being used", keyExpr);
            }
        }

        /// <summary>
        /// Find a set of all candidate Input System key codes that could be referred to by the given
        /// expression.
        /// </summary>
        private IEnumerable<RGActionParamFunc<Key>> FindCandidateInputSysKeyFuncs(ExpressionSyntax keyExpr)
        {
            bool TryMatch(ExpressionSyntax expr, out RGActionParamFunc<Key> keyFunc)
            {
                var sym = _currentModel.Value.GetSymbolInfo(expr).Symbol;
                if (sym != null)
                {
                    if (sym is IFieldSymbol fieldSym)
                    {
                        if (fieldSym.IsStatic && fieldSym.ContainingType?.TypeKind == TypeKind.Enum &&
                            FindType(fieldSym.ContainingType) == typeof(Key))
                        {
                            // constant key, e.g. Key.F2
                            var key = Enum.Parse<Key>(fieldSym.Name);
                            keyFunc = RGActionParamFunc<Key>.Constant(key);
                            return true;
                        }
                        else
                        {
                            // key stored in dynamic field
                            if (TryGetMemberAccessFunc(expr, out keyFunc))
                            {
                                return true;
                            }
                        }
                    }
                    else if (sym is IPropertySymbol)
                    {
                        if (TryGetMemberAccessFunc(expr, out keyFunc))
                        {
                            return true;
                        }
                    }
                }
                keyFunc = null;
                return false;
            }

            var matched = false;
            var keySym = _currentModel.Value.GetSymbolInfo(keyExpr).Symbol;
            if (keySym is ILocalSymbol localSym)
            {
                // key expression is local variable, check all assignments to the local
                foreach (var valueExpr in FindCandidateValuesForLocalVariable(localSym))
                {
                    if (TryMatch(valueExpr, out var keyFunc))
                    {
                        matched = true;
                        yield return keyFunc;
                    }
                }
            }
            else if (TryMatch(keyExpr, out var keyFunc))
            {
                matched = true;
                yield return keyFunc;
            }

            if (!matched)
            {
                AddAnalysisWarning("Could not identify key being used", keyExpr);
            }
        }

        /// <summary>
        /// Find the set of literal values that could be referred to by the given expression.
        /// Currently this supports either int or string.
        /// </summary>
        private IEnumerable<RGActionParamFunc<T>> FindCandidateLiteralFuncs<T>(ExpressionSyntax expr)
        {
            bool TryMatch(ExpressionSyntax matchExpr, out RGActionParamFunc<T> func)
            {
                var sym = _currentModel.Value.GetSymbolInfo(matchExpr).Symbol;
                if (sym != null)
                {
                    if (sym is IFieldSymbol or IPropertySymbol)
                    {
                        if (TryGetMemberAccessFunc(matchExpr, out func))
                        {
                            return true;
                        }
                    }
                }
                else if (matchExpr is LiteralExpressionSyntax literalExpr)
                {
                    if (typeof(T) == typeof(int))
                    {
                        if (literalExpr.Kind() == SyntaxKind.NumericLiteralExpression)
                        {
                            object value = int.Parse(literalExpr.Token.ValueText);
                            func = RGActionParamFunc<T>.Constant((T)value);
                            return true;
                        }
                    }
                    else if (typeof(T) == typeof(string))
                    {
                        if (literalExpr.Kind() == SyntaxKind.StringLiteralExpression)
                        {
                            func = RGActionParamFunc<T>.Constant((T)(object)literalExpr.Token.ValueText);
                            return true;
                        }
                    }
                    else
                    {
                        throw new ArgumentException();
                    }
                }
                func = null;
                return false;
            }

            var matched = false;
            var sym = _currentModel.Value.GetSymbolInfo(expr).Symbol;
            if (sym is ILocalSymbol localSym)
            {
                foreach (var valueExpr in FindCandidateValuesForLocalVariable(localSym))
                {
                    if (TryMatch(valueExpr, out var func))
                    {
                        matched = true;
                        yield return func;
                    }
                }
            }
            else if (TryMatch(expr, out var func))
            {
                matched = true;
                yield return func;
            }
            if (!matched)
            {
                AddAnalysisWarning($"Could not resolve {typeof(T).Name} expression", expr);
            }
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            base.VisitInvocationExpression(node);

            var nodeSymInfo = _currentModel.Value.GetSymbolInfo(node.Expression);
            if (nodeSymInfo.Symbol is IMethodSymbol methodSymbol)
            {
                var containingType = FindType(methodSymbol.ContainingType);

                // Legacy input manager
                if (containingType == typeof(Input))
                {
                    #if ENABLE_LEGACY_INPUT_MANAGER
                    var methodName = methodSymbol.Name;
                    switch (methodName)
                    {
                        // Input.GetKey(...), Input.GetKeyDown(...), Input.GetKeyUp(...)
                        case "GetKey":
                        case "GetKeyDown":
                        case "GetKeyUp":
                        {
                            var keyArg = node.ArgumentList.Arguments[0];
                            foreach (var keyFunc in FindCandidateLegacyKeyFuncs(keyArg.Expression))
                            {
                                var path = GetActionPathFromSyntaxNode(node, new[] { keyFunc.ToString() });
                                AddAction(new LegacyKeyAction(path, null, keyFunc), node);
                            }
                            break;
                        }

                        // Input.GetMouseButton(...), Input.GetMouseButtonDown(...), Input.GetMouseButtonUp(...)
                        case "GetMouseButton":
                        case "GetMouseButtonDown":
                        case "GetMouseButtonUp":
                        {
                            var btnArg = node.ArgumentList.Arguments[0];
                            foreach (var btnFunc in FindCandidateLiteralFuncs<int>(btnArg.Expression))
                            {
                                var path = GetActionPathFromSyntaxNode(node, new[] { btnFunc.ToString() });
                                AddAction(new MouseButtonAction(path, null, btnFunc), node);
                            }
                            break;
                        }

                        // Input.GetAxis(...), Input.GetAxisRaw(...)
                        case "GetAxis":
                        case "GetAxisRaw":
                        {
                            var axisArg = node.ArgumentList.Arguments[0];
                            foreach (var axisNameFunc in FindCandidateLiteralFuncs<string>(axisArg.Expression))
                            {
                                var path = GetActionPathFromSyntaxNode(node, new[] { axisNameFunc.ToString() });
                                AddAction(new LegacyAxisAction(path, null, axisNameFunc), node);
                            }
                            break;
                        }

                        // Input.GetButton(...), Input.GetButtonDown(...), Input.GetButtonUp(...)
                        case "GetButton":
                        case "GetButtonDown":
                        case "GetButtonUp":
                        {
                            var btnArg = node.ArgumentList.Arguments[0];
                            foreach (var btnNameFunc in FindCandidateLiteralFuncs<string>(btnArg.Expression))
                            {
                                var path = GetActionPathFromSyntaxNode(node, new[] { btnNameFunc.ToString() });
                                AddAction(new LegacyButtonAction(path, null, btnNameFunc), node);
                            }
                            break;
                        }
                    }
                    #endif
                }
                else if (containingType == typeof(Physics) || containingType == typeof(Physics2D))
                {
                    var posType = containingType == typeof(Physics)
                        ? MousePositionType.COLLIDER_3D
                        : MousePositionType.COLLIDER_2D;

                    var methodName = methodSymbol.Name;
                    // A call to Physics.Raycast or Physics2D.Raycast was discovered
                    switch (methodName)
                    {
                        // Three variants (Physics.Raycast, Physics.RaycastAll, Physics.RaycastNonAlloc)
                        case "Raycast":
                        case "RaycastAll":
                        case "RaycastNonAlloc":
                        {
                            var firstArg = node.ArgumentList.Arguments[0]; // first argument is either the origin point or ray
                            List<RGActionParamFunc<int>> layerMasks = null;
                            var didCheckLayerMask = false;
                            // Find any inputs that the point/ray could be derived from
                            foreach (var inpNode in FindDerivedUserInput(firstArg.Expression))
                            {
                                if (!didCheckLayerMask)
                                {
                                    // Examine the layerMask parameter (if any) in the Raycast invocation.
                                    // Compute all the values that it could be referring to and store them in the layerMasks list.
                                    // If any layer masks are present, then they will be used to further filter down the set of valid mouse coordinates.
                                    didCheckLayerMask = true;
                                    for (int argIndex = 0, numArgs = node.ArgumentList.Arguments.Count;
                                         argIndex < numArgs; ++argIndex)
                                    {
                                        // Check whether any argument parameter is named "layerMask"
                                        var paramSymbol = RGAnalysisUtils.FindArgumentParameter(node.ArgumentList, argIndex, methodSymbol);
                                        if (paramSymbol.Name == "layerMask")
                                        {
                                            // We've found the layer mask parameter, now try to identify all candidate integer values.
                                            layerMasks = new List<RGActionParamFunc<int>>();
                                            var layerMaskExpr = node.ArgumentList.Arguments[argIndex].Expression;
                                            foreach (var layerMaskFunc in FindCandidateLiteralFuncs<int>(layerMaskExpr))
                                            {
                                                layerMasks.Add(layerMaskFunc);
                                            }
                                        }
                                    }
                                }
                                var path = GetActionPathFromSyntaxNode(inpNode, GetActionPathFromSyntaxNode(node));
                                AddAction(new MousePositionAction(path, posType, layerMasks, null), inpNode);
                            }
                        }
                        break;
                    }
                }
                else
                {
                    // Add any unbound actions that are associated with this method invocation
                    var methodSig = methodSymbol.ToString();
                    if (_unboundActions.TryGetValue(methodSig, out var methodUnboundActions))
                    {
                        foreach (var act in methodUnboundActions.Values)
                        {
                            var path = GetActionPathFromSyntaxNode(node, act.Paths[0]);
                            var clonedAction = (RGGameAction)act.Clone();
                            clonedAction.Paths[0] = path;
                            AddAction(clonedAction, node);
                        }
                    }
                }
            }
        }

        public override void VisitBracketedArgumentList(BracketedArgumentListSyntax node)
        {
            base.VisitBracketedArgumentList(node);

            if (node.Parent is ElementAccessExpressionSyntax expr && node.Arguments.Count == 1)
            {
                var symInfo = _currentModel.Value.GetSymbolInfo(expr);
                if (symInfo.Symbol is IPropertySymbol propSym)
                {
                    var containingType = FindType(propSym.ContainingType);
                    if (containingType == typeof(Keyboard))
                    {
                        var arg = node.Arguments[0].Expression;
                        if (FindType(_currentModel.Value.GetTypeInfo(arg).Type) == typeof(Key))
                        {
                            // Bracketed key notation Keyboard.current[<key>]
                            foreach (var keyFunc in FindCandidateInputSysKeyFuncs(arg))
                            {
                                var path = GetActionPathFromSyntaxNode(node, new[] { keyFunc.ToString() });
                                AddAction(new InputSystemKeyAction(path, null, keyFunc), node);
                            }
                        }
                    }
                }
            }
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            base.VisitMemberAccessExpression(node);

            var sym = _currentModel.Value.GetSymbolInfo(node).Symbol;

            if (sym is IPropertySymbol propSym)
            {
                var type = FindType(propSym.ContainingType);
                if (type == typeof(Input))
                {
                    #if ENABLE_LEGACY_INPUT_MANAGER
                    switch (propSym.Name)
                    {
                        // Input.anyKey, Input.anyKeyDown
                        case "anyKey":
                        case "anyKeyDown":
                        {
                            var path = GetActionPathFromSyntaxNode(node);
                            AddAction(new AnyKeyAction(path, null), node);
                            break;
                        }

                        // Input.mousePosition
                        case "mousePosition":
                        {
                            var path = GetActionPathFromSyntaxNode(node);
                            AddAction(new MousePositionAction(path, null), node);
                            break;
                        }

                        // Input.mouseScrollDelta
                        case "mouseScrollDelta":
                        {
                            var path = GetActionPathFromSyntaxNode(node);
                            AddAction(new MouseScrollAction(path, null), node);
                            break;
                        }
                    }
                    #endif
                }
                else if (type == typeof(Keyboard))
                {
                    var exprType = FindType(_currentModel.Value.GetTypeInfo(node).Type);
                    if (exprType != null && typeof(ButtonControl).IsAssignableFrom(exprType))
                    {
                        // Keyboard.current.<property>
                        var path = GetActionPathFromSyntaxNode(node);
                        if (propSym.Name == "anyKey")
                        {
                            AddAction(new AnyKeyAction(path, null), node);
                        }
                        else
                        {
                            var key = RGActionManagerUtils.InputSystemKeyboardPropertyNameToKey(propSym.Name);
                            if (key == Key.None)
                            {
                                AddAnalysisWarning($"Unrecognized keyboard property '{propSym.Name}'", node);
                            }
                            AddAction(new InputSystemKeyAction(path, null, RGActionParamFunc<Key>.Constant(key)), node);
                        }
                    }
                }
                else if (type == typeof(Mouse))
                {
                    var exprType = FindType(_currentModel.Value.GetTypeInfo(node).Type);
                    if (exprType != null)
                    {
                        if (typeof(ButtonControl).IsAssignableFrom(exprType))
                        {
                            // Mouse.current.<button>
                            int mouseButton;
                            switch (propSym.Name)
                            {
                                case "leftButton":
                                    mouseButton = MouseButtonInput.LEFT_MOUSE_BUTTON;
                                    break;
                                case "middleButton":
                                    mouseButton = MouseButtonInput.MIDDLE_MOUSE_BUTTON;
                                    break;
                                case "rightButton":
                                    mouseButton = MouseButtonInput.RIGHT_MOUSE_BUTTON;
                                    break;
                                case "forwardButton":
                                    mouseButton = MouseButtonInput.FORWARD_MOUSE_BUTTON;
                                    break;
                                case "backButton":
                                    mouseButton = MouseButtonInput.BACK_MOUSE_BUTTON;
                                    break;
                                default:
                                    AddAnalysisWarning($"Unrecognized mouse property {propSym.Name}", node);
                                    return;
                            }
                            var path = GetActionPathFromSyntaxNode(node);
                            AddAction(new MouseButtonAction(path, null, RGActionParamFunc<int>.Constant(mouseButton)), node);
                        }
                        else if (typeof(DeltaControl).IsAssignableFrom(exprType))
                        {
                            if (propSym.Name == "scroll")
                            {
                                // Mouse.current.scroll
                                var path = GetActionPathFromSyntaxNode(node);
                                AddAction(new MouseScrollAction(path, null), node);
                            }
                        }
                    }
                }
                else if (type == typeof(UnityEngine.InputSystem.Pointer))
                {
                    var path = GetActionPathFromSyntaxNode(node);
                    switch (propSym.Name)
                    {
                        case "position":
                        case "delta":
                        {
                            AddAction(new MousePositionAction(path, null), node);
                            break;
                        }
                    }
                }
            }
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            base.VisitMethodDeclaration(node);

            var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl == null) return;
            var objectType = FindType(_currentModel.Value.GetDeclaredSymbol(classDecl));
            if (typeof(MonoBehaviour).IsAssignableFrom(objectType))
            {
                var declSym = _currentModel.Value.GetDeclaredSymbol(node);
                if (declSym.Parameters.Length == 0)
                {
                    #if ENABLE_LEGACY_INPUT_MANAGER
                    switch (declSym.Name)
                    {
                        // OnMouseOver(), OnMouseEnter(), OnMouseExit() handler
                        case "OnMouseOver":
                        case "OnMouseEnter":
                        case "OnMouseExit":
                        {
                            var path = new [] { objectType.FullName, declSym.Name };
                            AddAction(new MouseHoverObjectAction(path, objectType), node);
                            break;
                        }

                        // OnMouseDown(), OnMouseUp(), OnMouseUpAsButton(), OnMouseDrag() handler
                        case "OnMouseDown":
                        case "OnMouseUp":
                        case "OnMouseUpAsButton":
                        case "OnMouseDrag":
                        {
                            var path = new [] { objectType.FullName, declSym.Name };
                            AddAction(new MousePressObjectAction(path, objectType), node);
                            break;
                        }
                    }
                    #endif
                }
            }
        }

        private void AddAnalysisWarning(string message, SyntaxNode node = null)
        {
            _warnings.Add(new RGActionAnalysisWarning(message, node));
        }

        private string[] GetActionPathFromSyntaxNode(SyntaxNode node, string[] pathSuffix = null)
        {
            string typeName;
            var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl != null)
            {
                var typeSymbol = _currentModel.Value.GetDeclaredSymbol(classDecl);
                typeName = typeSymbol.ToString();
            }
            else
            {
                typeName = "<global>";
            }

            var filePath = _currentTree.Value.FilePath;

            var pathLen = 3;
            if (pathSuffix != null)
            {
                pathLen += pathSuffix.Length;
            }
            var path = new string[pathLen];

            path[0] = typeName;
            path[1] = Path.GetFileName(filePath);
            path[2] = node.ToString();
            if (pathSuffix != null)
            {
                for (var i = 0; i < pathSuffix.Length; ++i)
                {
                    path[3 + i] = pathSuffix[i];
                }
            }

            return path;
        }

        /// <summary>
        /// Adds the specified action associated with the given syntax node (optional).
        /// If the ObjectType is not specified on the action (null), then it is automatically inferred from
        /// the given syntax node.
        /// </summary>
        private void AddAction(RGGameAction action, SyntaxNode sourceNode)
        {
            if (action.ObjectType == null)
            {
                var classDecl = sourceNode.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (classDecl == null) return;
                var objectType = FindType(_currentModel.Value.GetDeclaredSymbol(classDecl));
                if (typeof(MonoBehaviour).IsAssignableFrom(objectType))
                {
                    action.ObjectType = objectType;
                }
            }
            if (action.ObjectType != null)
            {
                var path = string.Join("/", action.Paths[0]);
                if (_rawActions.TryAdd(path, action))
                {
                    if (sourceNode != null)
                    {
                        var nodeActions = _rawActionsByNode.GetOrAdd(sourceNode, new List<RGGameAction>());
                        nodeActions.Add(action);
                    }
                }
            }
            else
            {
                // If an action was identified outside a MonoBehaviour (e.g. in a helper method), then
                // it is added as an "unbound" action (i.e. it is unknown what component is listening for it).
                // The action is associated with its containing method. Eventually, if a method invocation is found
                // from a MonoBehaviour to the method that contained the actions, then copies of all the unbound actions
                // will be associated with the containing MonoBehaviour.
                var methodDecl = sourceNode.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (methodDecl == null)
                {
                    AddAnalysisWarning("Actions used outside of MonoBehaviour that are not contained in a method are not supported", sourceNode);
                    return;
                }
                var methodSym = _currentModel.Value.GetDeclaredSymbol(methodDecl);
                var methodSig = methodSym.ToString();

                var methodUnboundActions = _unboundActions.GetOrAdd(methodSig, new ConcurrentDictionary<string, RGGameAction>());

                var path = string.Join("/", action.Paths[0]);
                if (methodUnboundActions.TryAdd(path, action))
                {
                    _unboundActionsNeedResolution = true;
                }
            }
        }

        private static IEnumerable<GameObject> IterateGameObjects(GameObject gameObject)
        {
            yield return gameObject;
            foreach (Transform child in gameObject.transform)
            {
                foreach (var go in IterateGameObjects(child.gameObject))
                {
                    yield return go;
                }
            }
        }

        private static IEnumerable<GameObject> IterateGameObjects(Scene scn)
        {
            foreach (var rootGameObject in scn.GetRootGameObjects())
            {
                foreach (var go in IterateGameObjects(rootGameObject))
                {
                    yield return go;
                }
            }
        }

        private static bool IsRGOverlayObject(GameObject gameObject)
        {
            Transform t = gameObject.transform;
            while (t != null)
            {
                if (t.gameObject.name.Contains("RGOverlayCanvas"))
                {
                    return true;
                }
                t = t.parent;
            }
            return false;
        }

        private async Task RunCodeAnalysis(int passNum, List<(Assembly,Compilation)> targetAssemblies)
        {
            // some await to make this async on another thread right away
            await Task.CompletedTask;

            List<Task> tasks = new();
            // ReSharper disable once LoopCanBeConvertedToQuery - task thread indexing
            for (var i = 0; i < targetAssemblies.Count; ++i)
            {
                var myIndex = i;
                tasks.Add(Task.Run(async () =>
                {
                    // some await to make this async on another thread
                    await Task.CompletedTask;

                    var compilation = targetAssemblies[myIndex].Item2;
                    foreach (var syntaxTree in compilation.SyntaxTrees)
                    {

                        _currentModel.Value = compilation.GetSemanticModel(syntaxTree);
                        _currentTree.Value = syntaxTree;
                        _assignmentExprs.Value.Clear();
                        _localDeclarationStmts.Value.Clear();
                        var root = syntaxTree.GetCompilationUnitRoot();
                        Visit(root);
                    }
                }));
            }

            await Task.WhenAll(tasks.ToArray());
        }

        /// <summary>
        /// Identify any actions that may be defined by the game object.
        /// </summary>
        private void AnalyzeGameObject(GameObject gameObject)
        {
            // Check whether this game object has a Button component.
            // If so, then inspect all the button event listeners and generate actions for them.
            if (!IsRGOverlayObject(gameObject))
            {
                if (gameObject.TryGetComponent(out Button btn))
                {
                    foreach (var listener in RGActionManagerUtils.GetEventListenerMethodNames(btn.onClick))
                    {
                        string[] path = { "Unity UI", "Button", listener };
                        AddAction(new UIButtonPressAction(path, typeof(Button), listener), null);
                    }
                }

                if (gameObject.TryGetComponent(out Toggle _))
                {
                    string[] path = { "Unity UI", "Toggle", null };
                    var normName = UIObjectPressAction.GetNormalizedGameObjectName(gameObject.name);
                    string dropdownName = null;

                    // if this toggle is the child of a dropdown, inherit part of the identifier from the dropdown
                    Selectable dropdownParent = gameObject.transform.GetComponentInParent<Dropdown>(true);
                    if (dropdownParent == null)
                        dropdownParent = gameObject.transform.GetComponentInParent<TMP_Dropdown>(true);
                    if (dropdownParent != null)
                    {
                        dropdownName = UIObjectPressAction.GetNormalizedGameObjectName(dropdownParent.gameObject.name);
                        path[2] = dropdownName + " " + normName;
                    }
                    else
                    {
                        path[2] = normName;
                    }

                    AddAction(new UITogglePressAction(path, typeof(Toggle), normName, dropdownName), null);
                }

                if (gameObject.TryGetComponent(out Dropdown _))
                {
                    var normName = UIObjectPressAction.GetNormalizedGameObjectName(gameObject.name);
                    string[] path = { "Unity UI", "Dropdown", normName };
                    AddAction(new UIObjectPressAction(path, typeof(Dropdown), normName), null);
                }

                if (gameObject.TryGetComponent(out TMP_Dropdown _))
                {
                    var normName = UIObjectPressAction.GetNormalizedGameObjectName(gameObject.name);
                    string[] path = { "Unity UI", "TMP_Dropdown", normName };
                    AddAction(new UIObjectPressAction(path, typeof(TMP_Dropdown), normName), null);
                }

                if (gameObject.TryGetComponent(out InputField _))
                {
                    var normName = UIObjectPressAction.GetNormalizedGameObjectName(gameObject.name);
                    string[] pathPress = { "Unity UI", "Input Field", "Press", normName };
                    string[] pathTextEntry = { "Unity UI", "Input Field", "Text Entry", normName };
                    string[] pathTextSubmit = { "Unity UI", "Input Field", "Text Submit", normName };
                    AddAction(new UIObjectPressAction(pathPress, typeof(InputField), normName), null);
                    AddAction(new UIInputFieldTextEntryAction(pathTextEntry, typeof(InputField), normName), null);
                    AddAction(new UIInputFieldSubmitAction(pathTextSubmit, typeof(InputField), normName), null);
                }

                if (gameObject.TryGetComponent(out TMP_InputField _))
                {
                    var normName = UIObjectPressAction.GetNormalizedGameObjectName(gameObject.name);
                    string[] pathPress = { "Unity UI", "Input Field (TMP)", "Press", normName };
                    string[] pathTextEntry = { "Unity UI", "Input Field (TMP)", "Text Entry", normName };
                    string[] pathTextSubmit = { "Unity UI", "Input Field (TMP)", "Text Submit", normName };
                    AddAction(new UIObjectPressAction(pathPress, typeof(TMP_InputField), normName), null);
                    AddAction(new UIInputFieldTextEntryAction(pathTextEntry, typeof(TMP_InputField), normName), null);
                    AddAction(new UIInputFieldSubmitAction(pathTextSubmit, typeof(TMP_InputField), normName), null);
                }

                if (gameObject.TryGetComponent(out Slider _))
                {
                    var normName = UIObjectPressAction.GetNormalizedGameObjectName(gameObject.name);
                    string[] pathPress = { "Unity UI", "Slider", "Press", normName };
                    string[] pathRelease = { "Unity UI", "Slider", "Release", normName };
                    AddAction(new UISliderPressAction(pathPress, typeof(Slider), normName), null);
                    AddAction(new UISliderReleaseAction(pathRelease, typeof(Slider), normName), null);
                }

                if (gameObject.TryGetComponent(out Scrollbar _))
                {
                    var normName = UIObjectPressAction.GetNormalizedGameObjectName(gameObject.name);
                    string[] pathPress = { "Unity UI", "Scrollbar", "Press", normName };
                    string[] pathRelease = { "Unity UI", "Scrollbar", "Release", normName };
                    AddAction(new UISliderPressAction(pathPress, typeof(Scrollbar), normName), null);
                    AddAction(new UISliderReleaseAction(pathRelease, typeof(Scrollbar), normName), null);
                }

                // search for embedded InputActions
                foreach (var c in gameObject.GetComponents<Component>())
                {
                    if (c == null)
                    {
                        continue;
                    }

                    var type = c.GetType();

                    if (!_fieldInfoCache.TryGetValue(type, out var fieldInfos))
                    {
                        fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.Static |
                                                    BindingFlags.Instance);
                        _fieldInfoCache[type] = fieldInfos;
                    }

                    foreach (var fieldInfo in fieldInfos)
                    {
                        if (typeof(InputAction).IsAssignableFrom(fieldInfo.FieldType))
                        {
                            var action = (InputAction)fieldInfo.GetValue(c);
                            if (action != null)
                            {
                                AnalyzeInputAction(action, fieldInfo);
                            }
                        }
                    }

                    if (!_propertyInfoCache.TryGetValue(type, out var propInfos))
                    {
                        propInfos = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                                       BindingFlags.Static | BindingFlags.Instance);
                        _propertyInfoCache[type] = propInfos;
                    }

                    foreach (var propInfo in propInfos)
                    {
                        if (typeof(InputAction).IsAssignableFrom(propInfo.PropertyType))
                        {
                            var action = (InputAction)propInfo.GetValue(c);
                            if (action != null)
                            {
                                AnalyzeInputAction(action, propInfo);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generates an RGGameAction for the given InputAction.
        /// If this was an embedded action (directly defined in a component) rather than
        /// from an InputActionAsset, then the embeddedMember parameter is the
        /// member to access in order to obtain the InputAction at run time.
        /// </summary>
        private void AnalyzeInputAction(InputAction action, MemberInfo embeddedMember = null)
        {
            InputActionAction result;
            if (embeddedMember != null)
            {
                var path = new[] { embeddedMember.DeclaringType?.FullName, embeddedMember.Name };
                var actionFunc = RGActionParamFunc<InputAction>.MemberAccesses(new[] { embeddedMember });
                result = new InputActionAction(path, embeddedMember.DeclaringType, actionFunc, action);
            }
            else
            {
                var path = new[] { action.actionMap.asset.name, action.actionMap.name, action.name };
                result = new InputActionAction(path, action);
            }
            if (result.ParameterRange == null)
            {
                AddAnalysisWarning($"Unable to resolve parameter range for input action {string.Join("/", result.Paths[0])}");
                return;
            }
            AddAction(result, null);
        }

        /**
         * In Unity 6, EditorSceneManager.OpenPreviewScene is public, but before that you have to access the internal method with reflection
         */
        private static Scene OpenPreviewScene(string path)
        {
#if UNITY_6000_0_OR_NEWER
            return EditorSceneManager.OpenPreviewScene(path);
#else
            var openMethod = typeof(EditorSceneManager).GetMethod("OpenPreviewScene", BindingFlags.Static | BindingFlags.NonPublic, null, new [] {typeof(string)}, null);
            Scene result = (Scene) openMethod.Invoke(null, new object[] { path });
            return result;
#endif
        }

        private void RunResourceAnalysis()
        {
            _fieldInfoCache.Clear();
            _propertyInfoCache.Clear();

            const float resourceAnalysisStartProgress = 0.0f;
            const float resourceAnalysisEndProgress = 0.8f;

            NotifyProgress("Performing resource analysis", resourceAnalysisStartProgress);

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            var inputActionAssetGuids = AssetDatabase.FindAssets("t:InputActionAsset");
            var analyzedResourceCount = 0;
            var totalResourceCount = sceneGuids.Length + prefabGuids.Length + inputActionAssetGuids.Length;

            // Examine the game objects in all scenes in the project
            foreach (var sceneGuid in sceneGuids)
            {
                var progress = Mathf.Lerp(resourceAnalysisStartProgress, resourceAnalysisEndProgress,
                    analyzedResourceCount / (float)totalResourceCount);
                try
                {
                    var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                    if (scenePath.StartsWith("Packages/"))
                    {
                        continue;
                    }
                    NotifyProgress($"Performing resource analysis - Scene: {Path.GetFileNameWithoutExtension(scenePath)}", progress);

                    var scene = OpenPreviewScene(scenePath);
                    try
                    {
                        foreach (var gameObject in IterateGameObjects(scene))
                        {
                            AnalyzeGameObject(gameObject);
                        }
                    }
                    finally
                    {
                        EditorSceneManager.ClosePreviewScene(scene);
                    }
                }
                catch (Exception e)
                {
                    RGDebug.LogWarning("Exception when opening scene: " + e.Message + "\n" + e.StackTrace);
                }
                ++analyzedResourceCount;
            }

            // Examine all the prefabs in the project
            foreach (var prefabGuid in prefabGuids)
            {
                var progress = Mathf.Lerp(resourceAnalysisStartProgress, resourceAnalysisEndProgress,
                    analyzedResourceCount / (float)totalResourceCount);
                try
                {
                    var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                    if (prefabPath.StartsWith("Packages/"))
                    {
                        continue;
                    }
                    NotifyProgress($"Performing resource analysis - Prefab: {Path.GetFileNameWithoutExtension(prefabPath)}", progress);
                    var prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
                    foreach (var gameObject in IterateGameObjects(prefabContents))
                    {
                        AnalyzeGameObject(gameObject);
                    }
                    EditorSceneManager.ClosePreviewScene(prefabContents.scene);
                }
                catch (Exception e)
                {
                    RGDebug.LogWarning("Exception when opening prefab: " + e.Message + "\n" + e.StackTrace);
                }
                ++analyzedResourceCount;
            }

            // Examine all the InputActionAssets in the project
            foreach (var inputAssetGuid in inputActionAssetGuids)
            {
                try
                {
                    var progress = Mathf.Lerp(resourceAnalysisStartProgress, resourceAnalysisEndProgress,
                        analyzedResourceCount / (float)totalResourceCount);
                    var inputAssetPath = AssetDatabase.GUIDToAssetPath(inputAssetGuid);
                    if (inputAssetPath.StartsWith("Packages/"))
                    {
                        continue;
                    }

                    NotifyProgress($"Performing resource analysis - InputActionAsset: {Path.GetFileNameWithoutExtension(inputAssetPath)}", progress);
                    var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputAssetPath);
                    foreach (var actionMap in inputAsset.actionMaps)
                    {
                        foreach (var act in actionMap.actions)
                        {
                            // ReSharper disable once RedundantArgumentDefaultValue
                            AnalyzeInputAction(act, null);
                        }
                    }
                }
                catch (Exception e)
                {
                    RGDebug.LogWarning("Exception when opening input asset: " + e.Message + "\n" + e.StackTrace);
                }
                ++analyzedResourceCount;
            }
        }

        /// <summary>
        /// Conduct the action analysis and save the result to a file used by RGActionProvider.
        /// </summary>
        /// <returns>Whether the analysis was completed. This could be false if the user requested cancellation.</returns>
        public bool RunAnalysis()
        {
            try
            {
                _rawActions.Clear();
                _rawActionsByNode.Clear();
                _unboundActions.Clear();
                _actions.Clear();
                _warnings.Clear();
                _propertyInfoCache.Clear();
                _fieldInfoCache.Clear();
                _assignmentExprs.Value.Clear();
                _localDeclarationStmts.Value.Clear();

                // do these expensive operations 1 time, but they have to be on the main thread
                var targetAssemblies = new List<Assembly>(GetTargetAssemblies());

                NotifyProgress("Starting async code analysis", 0.00f);

                // start code analysis in background
                var codeTask = Task.Run(async () =>
                {
                    // put an await here so this actually goes on a separate thread
                    await Task.CompletedTask;
                    // do this expensive thing only once
                    var analysisAssemblies = targetAssemblies.Select(a => (a, GetCompilationForAssembly(a))).ToList();

                    /*
                     * REG-1829 added this loop support.
                     * It expands the range of games supported by updating RGActionAnalysis to support identifying actions even if the input-handling code resides outside of a MonoBehaviour (for example, in a helper method).
                     * This is done by iteratively propagating identified actions through the method calls until the analysis results no longer change (this unfortunately means the code analysis often requires >1 passes).
                     */
                    int passNum = 1;
                    do
                    {
                        _unboundActionsNeedResolution = false;
                        await RunCodeAnalysis(passNum, analysisAssemblies);
                        ++passNum;
                    } while (_unboundActionsNeedResolution);
                });

                // do the resource analysis in the foreground
                RunResourceAnalysis();

                // wait for code analysis to complete
                NotifyProgress("Waiting for code analysis to complete", 0.95f);

                Thread.Sleep(1000);

                Task.WaitAll(codeTask);

                NotifyProgress("Saving analysis results", 0.98f);

                // Heuristic: If a syntax node is associated with multiple MousePositionAction, remove the imprecise one that is initially added (NON_UI)
                foreach (var entry in _rawActionsByNode)
                {
                    var shouldRemoveNonUI = entry.Value.Any(act =>
                        act is MousePositionAction mpAct && mpAct.PositionType != MousePositionType.NON_UI);
                    if (shouldRemoveNonUI)
                    {
                        foreach (var act in entry.Value)
                        {
                            if (act is MousePositionAction { PositionType: MousePositionType.NON_UI } mpAct)
                            {
                                var path = string.Join("/", mpAct.Paths[0]);
                                _rawActions.TryRemove(path, out _);
                            }
                        }
                    }
                }

                // Compute the set of unique actions
                foreach (var rawAction in _rawActions.Values)
                {
                    var action = _actions.FirstOrDefault(a => a.IsEquivalentTo(rawAction));
                    if (action != null)
                    {
                        action.Paths.Add(rawAction.Paths[0]);
                    }
                    else
                    {
                        _actions.Add(rawAction);
                    }
                }

                SaveAnalysisResult();

                if (_warnings.Count > 0)
                {
                    var warningsMessage = new StringBuilder();
                    warningsMessage.AppendLine($"{_warnings.Count} warnings encountered during analysis:");
                    foreach (var warning in _warnings)
                    {
                        warningsMessage.AppendLine(warning.ToString());
                    }
                    RGDebug.LogWarning(warningsMessage.ToString());
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                ClearProgress();
            }
        }

        private void SaveAnalysisResult()
        {
            var result = new RGActionAnalysisResult
            {
                Actions = new List<RGGameAction>(_actions)
            };
            using var sw = new StreamWriter(RGActionProvider.ANALYSIS_RESULT_PATH);
            sw.Write(JsonConvert.SerializeObject(result, Formatting.Indented, RGActionProvider.JSON_CONVERTERS));
            sw.Close();
        }

        private void NotifyProgress(string message, float progress)
        {
            if (_displayProgressBar)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Action Analysis", message, progress))
                {
                    throw new OperationCanceledException("Analysis cancelled by user");
                }
            }
        }

        private void ClearProgress()
        {
            if (_displayProgressBar)
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
#endif
