#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
using RegressionGames.Editor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if ENABLE_LEGACY_INPUT_MANAGER
using RegressionGames.Editor.RGLegacyInputUtility;
#endif

namespace RegressionGames.ActionManager
{
    /// <summary>
    /// Class used to store any warnings that occur during the analysis.
    /// </summary>
    public class RGActionAnalysisWarning
    {
        public string FilePath { get; }
        public int StartLineNumber { get; }
        public int EndLineNumber { get; }
        public string Message { get; }

        public RGActionAnalysisWarning(string message, SyntaxNode node = null)
        {
            if (node != null)
            {
                FilePath = node.SyntaxTree.FilePath;
                var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
                StartLineNumber = lineSpan.StartLinePosition.Line;
                EndLineNumber = lineSpan.EndLinePosition.Line;
            }
            Message = message;
        }

        public override string ToString()
        {
            if (FilePath != null)
            {
                return $"{FilePath}:{StartLineNumber}:{EndLineNumber}: {Message}";
            }
            else
            {
                return Message;
            }
        }
    }

    public class RGActionAnalysis : CSharpSyntaxWalker
    {
        // actions that were identified in a method outside of a MonoBehaviour (mapping from method name -> action path -> action)
        private Dictionary<string, Dictionary<string, RGGameAction>> _unboundActions;  
        
        // Mapping from action path to action. Populated as the analysis proceeds.
        // This is considered a "raw" set of actions, in that it is possible that redundant/equivalent
        // actions are generated. The final output of the analysis combines any equivalent actions.
        private Dictionary<string, RGGameAction> _rawActions;

        // Mapping that associates syntax nodes with the actions that were identified at those points.
        private Dictionary<SyntaxNode, List<RGGameAction>> _rawActionsByNode;

        private Compilation _currentCompilation;
        private SemanticModel _currentModel;
        private SyntaxTree _currentTree;
        private Dictionary<AssignmentExpressionSyntax, DataFlowAnalysis> _assignmentExprs;
        private Dictionary<LocalDeclarationStatementSyntax, DataFlowAnalysis> _localDeclarationStmts;
        private bool _changed;

        public ISet<RGGameAction> Actions { get; private set; }
        public List<RGActionAnalysisWarning> Warnings { get; private set; }

        private bool _displayProgressBar;

        public RGActionAnalysis(bool displayProgressBar = false)
        {
            _displayProgressBar = displayProgressBar;
        }
        
        /// <summary>
        /// Returns the set of assembly names that should be ignored by the analysis
        /// </summary>
        private ISet<string> GetIgnoredAssemblyNames()
        {
            Assembly rgAssembly = RGEditorUtils.FindRGAssembly();
            Assembly rgEditorAssembly = FindRGEditorAssembly();
            if (rgAssembly == null || rgEditorAssembly == null)
            {
                return null;
            }

            // ignore RG SDK assemblies and their dependencies
            HashSet<string> result = new HashSet<string>();
            result.Add(Path.GetFileNameWithoutExtension(rgAssembly.outputPath));
            result.Add(Path.GetFileNameWithoutExtension(rgEditorAssembly.outputPath));
            foreach (string asmPath in rgAssembly.allReferences)
            {
                result.Add(Path.GetFileNameWithoutExtension(asmPath));
            }
            foreach (string asmPath in rgEditorAssembly.allReferences)
            {
                result.Add(Path.GetFileNameWithoutExtension(asmPath));
            }

            return result;
        }

        public static Assembly FindRGEditorAssembly()
        {
            var rgEditorAsmName = Path.GetFileName(typeof(RGActionAnalysis).Assembly.Location);
            Assembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            foreach (Assembly assembly in assemblies)
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
            ISet<string> ignoredAssemblyNames = GetIgnoredAssemblyNames();
            Assembly[] playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            foreach (var playerAsm in playerAssemblies)
            {
                string playerAsmName = Path.GetFileNameWithoutExtension(playerAsm.outputPath);
                if (ignoredAssemblyNames.Contains(playerAsmName)
                    || playerAsm.sourceFiles.Length == 0)
                {
                    continue;
                }
                bool shouldSkip = false;
                foreach (string sourceFile in playerAsm.sourceFiles)
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
            List<MetadataReference> references = new List<MetadataReference>();
            foreach (var playerAsmRef in asm.allReferences)
            {
                references.Add(MetadataReference.CreateFromFile(playerAsmRef));
            }

            CSharpParseOptions parseOptions = new CSharpParseOptions().WithPreprocessorSymbols(asm.defines);

            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
            foreach (string sourceFile in asm.sourceFiles)
            {
                using (StreamReader sr = new StreamReader(sourceFile))
                {
                    SyntaxTree tree = CSharpSyntaxTree.ParseText(sr.ReadToEnd(), parseOptions, path: sourceFile);
                    syntaxTrees.Add(tree);
                }
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
            if (_localDeclarationStmts == null)
            {
                var root = _currentTree.GetRoot();
                _assignmentExprs = new Dictionary<AssignmentExpressionSyntax, DataFlowAnalysis>();
                foreach (var assignExpr in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    _assignmentExprs.Add(assignExpr, _currentModel.AnalyzeDataFlow(assignExpr));
                }

                _localDeclarationStmts = new Dictionary<LocalDeclarationStatementSyntax, DataFlowAnalysis>();
                foreach (var declExpr in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
                {
                    _localDeclarationStmts.Add(declExpr, _currentModel.AnalyzeDataFlow(declExpr));
                }
            }
            foreach (var entry in _assignmentExprs)
            {
                if (entry.Value.WrittenInside.Any(v => v.Equals(localSym)))
                {
                    var assignExpr = entry.Key;
                    yield return assignExpr.Right;
                }
            }

            foreach (var entry in _localDeclarationStmts)
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
                    var sym = _currentModel.GetSymbolInfo(node).Symbol;
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
            List<MemberInfo> members = new List<MemberInfo>();
            var currentExpr = memberAccessExpr;
            for (;;)
            {
                var symbol = _currentModel.GetSymbolInfo(currentExpr).Symbol;
                if (symbol != null)
                {
                    MemberInfo member = null;
                    if (symbol is IFieldSymbol fieldSym)
                    {
                        Type type = FindType(fieldSym.ContainingType);
                        if (type == null)
                        {
                            memberAccessFunc = null;
                            return false;
                        }
                        member = type.GetField(fieldSym.Name,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    } else if (symbol is IPropertySymbol propSym)
                    {
                        Type type = FindType(propSym.ContainingType);
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

                if (currentExpr is MemberAccessExpressionSyntax memberExpr && memberExpr.Expression is not ThisExpressionSyntax)
                {
                    currentExpr = memberExpr.Expression;
                } else
                {
                    break;
                }
            }
            members.Reverse();

            // if the first field is not static, then its declaring type should match the class declaration
            if (members[0] is FieldInfo firstField && !firstField.IsStatic)
            {
                var containingClass = memberAccessExpr.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (containingClass == null || members[0].DeclaringType.Name != containingClass.Identifier.Text)
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
                var symbol = _currentModel.GetSymbolInfo(expr).Symbol;
                if (symbol != null)
                {
                    if (symbol is IFieldSymbol fieldSym)
                    {
                        if (fieldSym.IsStatic && fieldSym.ContainingType?.TypeKind == TypeKind.Enum && fieldSym.ContainingType?.ToString() == "UnityEngine.KeyCode")
                        {
                            // constant keycode (e.g. KeyCode.Space)
                            KeyCode keyCode = Enum.Parse<KeyCode>(fieldSym.Name);
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
                    } else if (symbol is IPropertySymbol && expr is MemberAccessExpressionSyntax memberAccessExpr)
                    {
                        // keycode is stored in a dynamic property
                        if (TryGetMemberAccessFunc(memberAccessExpr, out keyFunc))
                        {
                            return true;
                        }
                    }
                } else if (expr is LiteralExpressionSyntax literalExpr)
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

            bool matched = false;
            var keySym = _currentModel.GetSymbolInfo(keyExpr).Symbol;
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
                var sym = _currentModel.GetSymbolInfo(expr).Symbol;
                if (sym != null)
                {
                    if (sym is IFieldSymbol fieldSym)
                    {
                        if (fieldSym.IsStatic && fieldSym.ContainingType?.TypeKind == TypeKind.Enum &&
                            FindType(fieldSym.ContainingType) == typeof(Key))
                        {
                            // constant key, e.g. Key.F2
                            Key key = Enum.Parse<Key>(fieldSym.Name);
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

            bool matched = false;
            var keySym = _currentModel.GetSymbolInfo(keyExpr).Symbol;
            if (keySym != null && keySym is ILocalSymbol localSym)
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
            bool TryMatch(ExpressionSyntax expr, out RGActionParamFunc<T> func)
            {
                var sym = _currentModel.GetSymbolInfo(expr).Symbol;
                if (sym != null)
                {
                    if (sym is IFieldSymbol or IPropertySymbol)
                    {
                        if (TryGetMemberAccessFunc(expr, out func))
                        {
                            return true;
                        }
                    }
                } else if (expr is LiteralExpressionSyntax literalExpr)
                {
                    if (typeof(T) == typeof(int))
                    {
                        if (literalExpr.Kind() == SyntaxKind.NumericLiteralExpression)
                        {
                            object value = int.Parse(literalExpr.Token.ValueText);
                            func = RGActionParamFunc<T>.Constant((T)value);
                            return true;
                        }
                    } else if (typeof(T) == typeof(string))
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

            bool matched = false;
            var sym = _currentModel.GetSymbolInfo(expr).Symbol;
            if (sym != null && sym is ILocalSymbol localSym)
            {
                foreach (var valueExpr in FindCandidateValuesForLocalVariable(localSym))
                {
                    if (TryMatch(valueExpr, out var func))
                    {
                        matched = true;
                        yield return func;
                    }
                }
            } else if (TryMatch(expr, out var func))
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

            var nodeSymInfo = _currentModel.GetSymbolInfo(node.Expression);
            if (nodeSymInfo.Symbol is IMethodSymbol methodSymbol)
            {
                var containingType = FindType(methodSymbol.ContainingType);

                // Legacy input manager
                if (containingType == typeof(UnityEngine.Input))
                {
                    #if ENABLE_LEGACY_INPUT_MANAGER
                    string methodName = methodSymbol.Name;
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
                                string[] path = GetActionPathFromSyntaxNode(node, new[] { keyFunc.ToString() });
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
                                string[] path = GetActionPathFromSyntaxNode(node, new[] { btnFunc.ToString() });
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
                                string[] path = GetActionPathFromSyntaxNode(node, new[] { axisNameFunc.ToString() });
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
                                string[] path = GetActionPathFromSyntaxNode(node, new[] { btnNameFunc.ToString() });
                                AddAction(new LegacyButtonAction(path, null, btnNameFunc), node);
                            }
                            break;
                        }
                    }
                    #endif
                } 
                else if (containingType == typeof(Physics) || containingType == typeof(Physics2D))
                {
                    MousePositionType posType = containingType == typeof(Physics)
                        ? MousePositionType.COLLIDER_3D
                        : MousePositionType.COLLIDER_2D;

                    string methodName = methodSymbol.Name;
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
                            bool didCheckLayerMask = false;
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
                                string[] path = GetActionPathFromSyntaxNode(inpNode, GetActionPathFromSyntaxNode(node));
                                AddAction(new MousePositionAction(path, posType, layerMasks, null), inpNode);
                            }
                        }
                        break;
                    }
                }
                else
                {
                    // Add any unbound actions that are associated with this method invocation
                    string methodSig = methodSymbol.ToString();
                    if (_unboundActions.TryGetValue(methodSig, out var methodUnboundActions))
                    {
                        foreach (var act in methodUnboundActions.Values)
                        {
                            string[] path = GetActionPathFromSyntaxNode(node, act.Paths[0]);
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

            if (node.Parent != null && node.Parent is ElementAccessExpressionSyntax expr && node.Arguments.Count == 1)
            {
                var symInfo = _currentModel.GetSymbolInfo(expr);
                if (symInfo.Symbol != null && symInfo.Symbol is IPropertySymbol propSym)
                {
                    var containingType = FindType(propSym.ContainingType);
                    if (containingType == typeof(Keyboard))
                    {
                        var arg = node.Arguments[0].Expression;
                        if (FindType(_currentModel.GetTypeInfo(arg).Type) == typeof(Key))
                        {
                            // Bracketed key notation Keyboard.current[<key>]
                            foreach (var keyFunc in FindCandidateInputSysKeyFuncs(arg))
                            {
                                string[] path = GetActionPathFromSyntaxNode(node, new[] { keyFunc.ToString() });
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

            var sym = _currentModel.GetSymbolInfo(node).Symbol;

            if (sym is IPropertySymbol propSym)
            {
                var type = FindType(propSym.ContainingType);
                if (type == typeof(UnityEngine.Input))
                {
                    #if ENABLE_LEGACY_INPUT_MANAGER
                    switch (propSym.Name)
                    {
                        // Input.anyKey, Input.anyKeyDown
                        case "anyKey":
                        case "anyKeyDown":
                        {
                            string[] path = GetActionPathFromSyntaxNode(node);
                            AddAction(new AnyKeyAction(path, null), node);
                            break;
                        }

                        // Input.mousePosition
                        case "mousePosition":
                        {
                            string[] path = GetActionPathFromSyntaxNode(node);
                            AddAction(new MousePositionAction(path, null), node);
                            break;
                        }

                        // Input.mouseScrollDelta
                        case "mouseScrollDelta":
                        {
                            string[] path = GetActionPathFromSyntaxNode(node);
                            AddAction(new MouseScrollAction(path, null), node);
                            break;
                        }
                    }
                    #endif
                } else if (type == typeof(Keyboard))
                {
                    var exprType = FindType(_currentModel.GetTypeInfo(node).Type);
                    if (exprType != null && typeof(ButtonControl).IsAssignableFrom(exprType))
                    {
                        // Keyboard.current.<property>
                        string[] path = GetActionPathFromSyntaxNode(node);
                        if (propSym.Name == "anyKey")
                        {
                            AddAction(new AnyKeyAction(path, null), node);
                        }
                        else
                        {
                            Key key = RGActionManagerUtils.InputSystemKeyboardPropertyNameToKey(propSym.Name);
                            if (key == Key.None)
                            {
                                AddAnalysisWarning($"Unrecognized keyboard property '{propSym.Name}'", node);
                            }
                            AddAction(new InputSystemKeyAction(path, null, RGActionParamFunc<Key>.Constant(key)), node);
                        }
                    }
                } else if (type == typeof(Mouse))
                {
                    var exprType = FindType(_currentModel.GetTypeInfo(node).Type);
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
                            string[] path = GetActionPathFromSyntaxNode(node);
                            AddAction(new MouseButtonAction(path, null, RGActionParamFunc<int>.Constant(mouseButton)), node);
                        } else if (typeof(DeltaControl).IsAssignableFrom(exprType))
                        {
                            if (propSym.Name == "scroll")
                            {
                                // Mouse.current.scroll
                                string[] path = GetActionPathFromSyntaxNode(node);
                                AddAction(new MouseScrollAction(path, null), node);
                            }
                        }
                    }
                } else if (type == typeof(UnityEngine.InputSystem.Pointer))
                {
                    string[] path = GetActionPathFromSyntaxNode(node);
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
            Type objectType = FindType(_currentModel.GetDeclaredSymbol(classDecl));
            if (typeof(MonoBehaviour).IsAssignableFrom(objectType))
            {
                var declSym = _currentModel.GetDeclaredSymbol(node);
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
                            string[] path = new string[] { objectType.FullName, declSym.Name };
                            AddAction(new MouseHoverObjectAction(path, objectType), node);
                            break;
                        }

                        // OnMouseDown(), OnMouseUp(), OnMouseUpAsButton(), OnMouseDrag() handler
                        case "OnMouseDown":
                        case "OnMouseUp":
                        case "OnMouseUpAsButton":
                        case "OnMouseDrag":
                        {
                            string[] path = new string[] { objectType.FullName, declSym.Name };
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
            Warnings.Add(new RGActionAnalysisWarning(message, node));
        }
        
        private string[] GetActionPathFromSyntaxNode(SyntaxNode node, string[] pathSuffix = null)
        {
            string typeName;
            var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl != null)
            {
                var typeSymbol = _currentModel.GetDeclaredSymbol(classDecl);
                typeName = typeSymbol.ToString();
            }
            else
            {
                typeName = "<global>";
            }
            
            var filePath = _currentTree.FilePath;
            var lineSpan = _currentTree.GetLineSpan(node.Span);
            int startLine = lineSpan.StartLinePosition.Line;
            int startChar = lineSpan.StartLinePosition.Character;
            int endLine = lineSpan.EndLinePosition.Line;
            int endChar = lineSpan.EndLinePosition.Character;

            int pathLen = 3;
            if (pathSuffix != null)
            {
                pathLen += pathSuffix.Length;
            }
            string[] path = new string[pathLen];

            path[0] = typeName;
            path[1] = Path.GetFileName(filePath) + ":" + startLine + ":" + startChar +
                      ":" + endLine + ":" + endChar;
            path[2] = node.ToString();
            if (pathSuffix != null)
            {
                for (int i = 0; i < pathSuffix.Length; ++i)
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
                Type objectType = FindType(_currentModel.GetDeclaredSymbol(classDecl));
                if (typeof(MonoBehaviour).IsAssignableFrom(objectType))
                {
                    action.ObjectType = objectType;
                }
            }
            if (action.ObjectType != null)
            {
                string path = string.Join("/", action.Paths[0]);
                if (_rawActions.TryAdd(path, action))
                {
                    if (sourceNode != null)
                    {
                        if (!_rawActionsByNode.TryGetValue(sourceNode, out var nodeActions))
                        {
                            nodeActions = new List<RGGameAction>();
                            _rawActionsByNode.Add(sourceNode, nodeActions);
                        }
                        nodeActions.Add(action);
                    }
                    _changed = true;
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
                var methodSym = _currentModel.GetDeclaredSymbol(methodDecl);
                string methodSig = methodSym.ToString();
                if (!_unboundActions.TryGetValue(methodSig, out var methodUnboundActions))
                {
                    methodUnboundActions = new Dictionary<string, RGGameAction>();
                    _unboundActions.Add(methodSig, methodUnboundActions);
                }
                string path = string.Join("/", action.Paths[0]);
                if (methodUnboundActions.TryAdd(path, action))
                {
                    _changed = true;
                }
            }
        }
        
        private static IEnumerable<GameObject> IterateGameObjects(GameObject gameObject)
        {
            yield return gameObject;
            foreach (Transform child in gameObject.transform)
            {
                foreach (GameObject go in IterateGameObjects(child.gameObject))
                {
                    yield return go;
                }
            }
        }

        private static IEnumerable<GameObject> IterateGameObjects(UnityEngine.SceneManagement.Scene scn)
        {
            foreach (GameObject rootGameObject in scn.GetRootGameObjects())
            {
                foreach (GameObject go in IterateGameObjects(rootGameObject))
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
        
        private void RunCodeAnalysis(int passNum)
        {
            const float codeAnalysisStartProgress = 0.0f;
            const float codeAnalysisEndProgress = 0.6f;
            NotifyProgress($"Performing code analysis (pass {passNum})", codeAnalysisStartProgress);
            var targetAssemblies = new List<Assembly>(GetTargetAssemblies());
            for (int i = 0; i < targetAssemblies.Count; ++i)
            {
                float asmStartProgress = Mathf.Lerp(codeAnalysisStartProgress, codeAnalysisEndProgress,
                    i / (float)targetAssemblies.Count);
                float asmEndProgress = Mathf.Lerp(codeAnalysisStartProgress, codeAnalysisEndProgress,
                    (i + 1) / (float)targetAssemblies.Count);

                Assembly asm = targetAssemblies[i];
                int numSyntaxTrees = asm.sourceFiles.Length;
                Compilation compilation = GetCompilationForAssembly(asm);
                _currentCompilation = compilation;
                int syntaxTreeIndex = 0;
                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    float progress = Mathf.Lerp(asmStartProgress, asmEndProgress, syntaxTreeIndex / (float)numSyntaxTrees);
                    NotifyProgress($"Analyzing {asm.name} (pass {passNum})", progress);
                    _currentModel = compilation.GetSemanticModel(syntaxTree);
                    _currentTree = syntaxTree;
                    _assignmentExprs = null;
                    _localDeclarationStmts = null;
                    var root = syntaxTree.GetCompilationUnitRoot();
                    Visit(root);
                    ++syntaxTreeIndex;
                }
            }
        }

        private ISet<string> _buttonClickListeners;

        /// <summary>
        /// Identify any actions that may be defined by the game object.
        /// </summary>
        private void AnalyzeGameObject(GameObject gameObject)
        {
            // Check whether this game object has a Button component.
            // If so, then inspect all the button event listeners and generate actions for them.
            if (gameObject.TryGetComponent(out Button btn) && !IsRGOverlayObject(gameObject))
            {
                foreach (string listener in RGActionManagerUtils.GetEventListenerMethodNames(btn.onClick))
                {
                    _buttonClickListeners.Add(listener);
                }
            }

            // search for embedded InputActions
            foreach (Component c in gameObject.GetComponents<Component>())
            {
                if (c == null)
                {
                    continue;
                }

                Type type = c.GetType();
                foreach (var fieldInfo in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                         BindingFlags.Static |
                                                         BindingFlags.Instance))
                {
                    if (typeof(InputAction).IsAssignableFrom(fieldInfo.FieldType))
                    {
                        InputAction action = (InputAction)fieldInfo.GetValue(c);
                        if (action != null)
                        {
                            AnalyzeInputAction(action, fieldInfo);
                        }
                    }
                }

                foreach (var propInfo in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                                        BindingFlags.Static | BindingFlags.Instance))
                {
                    if (typeof(InputAction).IsAssignableFrom(propInfo.PropertyType))
                    {
                        InputAction action = (InputAction)propInfo.GetValue(c);
                        if (action != null)
                        {
                            AnalyzeInputAction(action, propInfo);
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
                string[] path = new[] { embeddedMember.DeclaringType.FullName, embeddedMember.Name };
                var actionFunc = RGActionParamFunc<InputAction>.MemberAccesses(new[] { embeddedMember });
                result = new InputActionAction(path, embeddedMember.DeclaringType, actionFunc, action);
            }
            else
            {
                string[] path = new[] { action.actionMap.asset.name, action.actionMap.name, action.name };
                result = new InputActionAction(path, action);
            }
            if (result.ParameterRange == null)
            {
                AddAnalysisWarning($"Unable to resolve parameter range for input action {string.Join("/", result.Paths[0])}");
                return;
            }
            AddAction(result, null);
        }

        private void RunResourceAnalysis()
        {
            const float resourceAnalysisStartProgress = 0.6f;
            const float resourceAnalysisEndProgress = 0.9f;
            string origScenePath = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
            NotifyProgress("Performing resource analysis", resourceAnalysisStartProgress);
            try
            {
                _buttonClickListeners = new HashSet<string>();
                string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                string[] inputActionAssetGuids = AssetDatabase.FindAssets("t:InputActionAsset");
                int analyzedResourceCount = 0;
                int totalResourceCount = sceneGuids.Length + prefabGuids.Length + inputActionAssetGuids.Length;
                
                // Examine the game objects in all scenes in the project
                foreach (string sceneGuid in sceneGuids)
                {
                    float progress = Mathf.Lerp(resourceAnalysisStartProgress, resourceAnalysisEndProgress,
                        analyzedResourceCount / (float)totalResourceCount);
                    try
                    {
                        string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                        if (scenePath.StartsWith("Packages/"))
                        {
                            continue;
                        }
                        NotifyProgress($"Analyzing {Path.GetFileNameWithoutExtension(scenePath)}", progress);
                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
                        for (int i = 0, n = SceneManager.sceneCount; i < n; ++i)
                        {
                            var scene = SceneManager.GetSceneAt(i);
                            foreach (GameObject gameObject in IterateGameObjects(scene))
                            {
                                AnalyzeGameObject(gameObject);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        RGDebug.LogWarning("Exception when opening scene: " + e.Message + "\n" + e.StackTrace);
                    }
                    ++analyzedResourceCount;
                }

                // Examine all the prefabs in the project
                foreach (string prefabGuid in prefabGuids)
                {
                    float progress = Mathf.Lerp(resourceAnalysisStartProgress, resourceAnalysisEndProgress,
                        analyzedResourceCount / (float)totalResourceCount);
                    try
                    {
                        string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                        if (prefabPath.StartsWith("Packages/"))
                        {
                            continue;
                        }
                        NotifyProgress($"Analyzing {Path.GetFileNameWithoutExtension(prefabPath)}", progress);
                        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
                        foreach (GameObject gameObject in IterateGameObjects(prefabContents))
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
                foreach (string inputAssetGuid in inputActionAssetGuids)
                {
                    try
                    {
                        float progress = Mathf.Lerp(resourceAnalysisStartProgress, resourceAnalysisEndProgress,
                            analyzedResourceCount / (float)totalResourceCount);
                        string inputAssetPath = AssetDatabase.GUIDToAssetPath(inputAssetGuid);
                        if (inputAssetPath.StartsWith("Packages/"))
                        {
                            continue;
                        }

                        NotifyProgress($"Analyzing {Path.GetFileNameWithoutExtension(inputAssetPath)}", progress);
                        var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputAssetPath);
                        foreach (var actionMap in inputAsset.actionMaps)
                        {
                            foreach (var act in actionMap.actions)
                            {
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

                // Generate actions for the identified button click events
                foreach (string btnClickListener in _buttonClickListeners)
                {
                    string[] path = {"Unity UI", "Button Click", btnClickListener};
                    AddAction(new UIButtonPressAction(path, typeof(Button), btnClickListener), null);
                }
            }
            finally
            {
                // restore the scene that was originally opened
                if (!string.IsNullOrEmpty(origScenePath))
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(origScenePath);
                }
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
                _rawActions = new Dictionary<string, RGGameAction>();
                _rawActionsByNode = new Dictionary<SyntaxNode, List<RGGameAction>>();
                _unboundActions = new Dictionary<string, Dictionary<string, RGGameAction>>();
                Warnings = new List<RGActionAnalysisWarning>();

                {
                    int passNum = 1;
                    do
                    {
                        _changed = false;
                        RunCodeAnalysis(passNum);
                        ++passNum;
                    } while (_changed);
                }

                RunResourceAnalysis();

                NotifyProgress("Saving analysis results", 0.9f);

                // Heuristic: If a syntax node is associated with multiple MousePositionAction, remove the imprecise one that is initially added (NON_UI)
                foreach (var entry in _rawActionsByNode)
                {
                    bool shouldRemoveNonUI = entry.Value.Any(act =>
                        act is MousePositionAction mpAct && mpAct.PositionType != MousePositionType.NON_UI);
                    if (shouldRemoveNonUI)
                    {
                        foreach (var act in entry.Value)
                        {
                            if (act is MousePositionAction mpAct && mpAct.PositionType == MousePositionType.NON_UI)
                            {
                                string path = string.Join("/", mpAct.Paths[0]);
                                _rawActions.Remove(path);
                            }
                        }
                    }
                }

                // Compute the set of unique actions
                var actions = new HashSet<RGGameAction>();
                foreach (var rawAction in _rawActions.Values)
                {
                    var action = actions.FirstOrDefault(a => a.IsEquivalentTo(rawAction));
                    if (action != null)
                        action.Paths.Add(rawAction.Paths[0]);
                    else
                        actions.Add(rawAction);
                }

                Actions = actions;
                SaveAnalysisResult();

                if (Warnings.Count > 0)
                {
                    StringBuilder warningsMessage = new StringBuilder();
                    warningsMessage.AppendLine($"{Warnings.Count} warnings encountered during analysis:");
                    foreach (var warning in Warnings)
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
            RGActionAnalysisResult result = new RGActionAnalysisResult();
            result.Actions = new List<RGGameAction>(Actions);
            using (StreamWriter sw = new StreamWriter(RGActionProvider.ANALYSIS_RESULT_PATH))
            {
                sw.Write(JsonConvert.SerializeObject(result, Formatting.Indented, RGActionProvider.JSON_CONVERTERS));
            }
        }

        // Returns true if the analysis was requested to be cancelled
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
