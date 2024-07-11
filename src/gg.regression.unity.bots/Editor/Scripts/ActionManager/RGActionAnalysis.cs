﻿#if UNITY_EDITOR
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
using RegressionGames.Editor.RGLegacyInputUtility;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Assembly = UnityEditor.Compilation.Assembly;
using Button = UnityEngine.UI.Button;
using Newtonsoft.Json;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace RegressionGames.ActionManager
{
    public class RGActionAnalysisWarning
    {
        public string FilePath { get; }
        public int StartLineNumber { get; }
        public int EndLineNumber { get; }
        public string Message { get; }

        public RGActionAnalysisWarning(SyntaxNode node, string message)
        {
            FilePath = node.SyntaxTree.FilePath;
            var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
            StartLineNumber = lineSpan.StartLinePosition.Line;
            EndLineNumber = lineSpan.EndLinePosition.Line;
            Message = message;
        }

        public override string ToString()
        {
            return $"{FilePath}:{StartLineNumber}:{EndLineNumber}: {Message}";
        }
    }
    
    public class RGActionAnalysis : CSharpSyntaxWalker
    {
        private Dictionary<string, RGGameAction> _rawActions;
        private Compilation _currentCompilation;
        private SemanticModel _currentModel;
        private SyntaxTree _currentTree;
        private Dictionary<AssignmentExpressionSyntax, DataFlowAnalysis> _assignmentExprs;
        private Dictionary<LocalDeclarationStatementSyntax, DataFlowAnalysis> _localDeclarationStmts;

        public ISet<RGGameAction> Actions { get; private set; }
        public List<RGActionAnalysisWarning> Warnings { get; private set; }

        private bool _displayProgressBar;

        public RGActionAnalysis(bool displayProgressBar = false)
        {
            _displayProgressBar = displayProgressBar;
        }

        private ISet<string> GetIgnoredAssemblyNames()
        {
            Assembly rgAssembly = RGLegacyInputInstrumentation.FindRGAssembly();
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
                        member = type.GetField(fieldSym.Name,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    } else if (symbol is IPropertySymbol propSym)
                    {
                        Type type = FindType(propSym.ContainingType);
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
                AddAnalysisWarning(keyExpr, "Could not identify key code being used");
            }
        }

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
                AddAnalysisWarning(keyExpr, "Could not identify key being used");
            }
        }

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
                AddAnalysisWarning(expr, $"Could not resolve {typeof(T).Name} expression");
            }
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var nodeSymInfo = _currentModel.GetSymbolInfo(node.Expression);
            if (nodeSymInfo.Symbol is IMethodSymbol methodSymbol)
            {
                var containingType = FindType(methodSymbol.ContainingType);
                
                // Legacy input manager
                if (containingType == typeof(UnityEngine.Input))
                {
                    var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                    if (classDecl == null) return;
                    Type objectType = FindType(_currentModel.GetDeclaredSymbol(classDecl));
                    if (!typeof(MonoBehaviour).IsAssignableFrom(objectType))
                    {
                        AddAnalysisWarning(node, "Inputs handled outside of a MonoBehaviour are not supported");
                        return;
                    }
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
                                string[] path = { objectType.FullName, $"Input.{methodName}({keyFunc})" };
                                AddAction(new LegacyKeyAction(path, objectType, keyFunc));
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
                                string[] path = { objectType.FullName, $"Input.{methodName}({btnFunc})" };
                                AddAction(new MouseButtonAction(path, objectType, btnFunc));
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
                                string[] path = { objectType.FullName, $"Input.{methodName}({axisNameFunc})" };
                                AddAction(new LegacyAxisAction(path, objectType, axisNameFunc));
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
                                string[] path = { objectType.FullName, $"Input.{methodName}({btnNameFunc})" };
                                AddAction(new LegacyButtonAction(path, objectType, btnNameFunc));
                            }
                            break;
                        }
                    }
                }
            }
            
            base.VisitInvocationExpression(node);
        }

        public override void VisitBracketedArgumentList(BracketedArgumentListSyntax node)
        {
            if (node.Parent != null && node.Parent is ElementAccessExpressionSyntax expr && node.Arguments.Count == 1)
            {
                var symInfo = _currentModel.GetSymbolInfo(expr);
                if (symInfo.Symbol != null && symInfo.Symbol is IPropertySymbol propSym)
                {
                    var containingType = FindType(propSym.ContainingType);
                    if (containingType == typeof(Keyboard))
                    {
                        var arg = node.Arguments[0].Expression;
                        var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                        if (classDecl == null) return;
                        Type objectType = FindType(_currentModel.GetDeclaredSymbol(classDecl));
                        if (!typeof(MonoBehaviour).IsAssignableFrom(objectType))
                        {
                            AddAnalysisWarning(node, "Inputs handled outside of a MonoBehaviour are not supported");
                            return;
                        }
                        if (FindType(_currentModel.GetTypeInfo(arg).Type) == typeof(Key))
                        {
                            // Bracketed key notation Keyboard.current[<key>]
                            foreach (var keyFunc in FindCandidateInputSysKeyFuncs(arg))
                            {
                                string[] path = { objectType.FullName, $"Keyboard.current[{keyFunc}]" };
                                AddAction(new InputSystemKeyAction(path, objectType, keyFunc));
                            }
                        }
                    }
                }
            }
            
            base.VisitBracketedArgumentList(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var sym = _currentModel.GetSymbolInfo(node).Symbol;

            if (sym is IPropertySymbol propSym)
            {
                var type = FindType(propSym.ContainingType);
                if (type == typeof(UnityEngine.Input))
                {
                    var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                    if (classDecl == null) return;
                    Type objectType = FindType(_currentModel.GetDeclaredSymbol(classDecl));
                    if (!typeof(MonoBehaviour).IsAssignableFrom(objectType))
                    {
                        AddAnalysisWarning(node, "Inputs handled outside of a MonoBehaviour are not supported");
                        return;
                    }
                    switch (propSym.Name)
                    {
                        // Input.anyKey, Input.anyKeyDown
                        case "anyKey":
                        case "anyKeyDown":
                        {
                            string[] path = new string[] { objectType.FullName, $"Input.{propSym.Name}" };
                            AddAction(new AnyKeyAction(path, objectType));
                            break;
                        }
                        
                        // Input.mousePosition
                        case "mousePosition":
                        {
                            string[] path = new string[] { objectType.FullName, $"Input.mousePosition" };
                            AddAction(new MousePositionAction(path, objectType));
                            break;
                        }
                        
                        // Input.mouseScrollDelta
                        case "mouseScrollDelta":
                        {
                            string[] path = new string[] { objectType.FullName, "Input.mouseScrollDelta" };
                            AddAction(new MouseScrollAction(path, objectType));
                            break;
                        }
                    }
                } else if (type == typeof(Keyboard))
                {
                    var exprType = FindType(_currentModel.GetTypeInfo(node).Type);
                    if (exprType != null && typeof(ButtonControl).IsAssignableFrom(exprType))
                    {
                        // Keyboard.current.<property>
                        var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                        if (classDecl == null) return;
                        Type objectType = FindType(_currentModel.GetDeclaredSymbol(classDecl));
                        if (!typeof(MonoBehaviour).IsAssignableFrom(objectType))
                        {
                            AddAnalysisWarning(node, "Inputs handled outside of a MonoBehaviour are not supported");
                            return;
                        }
                        string[] path = { objectType.FullName, $"Keyboard.current.{propSym.Name}" };
                        if (propSym.Name == "anyKey")
                        {
                            AddAction(new AnyKeyAction(path, objectType));
                        }
                        else
                        {
                            Key key = RGActionManagerUtils.InputSystemKeyboardPropertyNameToKey(propSym.Name);
                            if (key == Key.None)
                            {
                                AddAnalysisWarning(node, $"Unrecognized keyboard property '{propSym.Name}'");
                            }
                            AddAction(new InputSystemKeyAction(path, objectType, RGActionParamFunc<Key>.Constant(key)));
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
                            var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                            if (classDecl == null) return;
                            Type objectType = FindType(_currentModel.GetDeclaredSymbol(classDecl));
                            if (!typeof(MonoBehaviour).IsAssignableFrom(objectType))
                            {
                                AddAnalysisWarning(node, "Inputs handled outside of a MonoBehaviour are not supported");
                                return;
                            }
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
                                    AddAnalysisWarning(node, $"Unrecognized mouse property {propSym.Name}");
                                    return;
                            }
                            string[] path = { objectType.FullName, $"Mouse.current.{propSym.Name}" };
                            AddAction(new MouseButtonAction(path, objectType, RGActionParamFunc<int>.Constant(mouseButton)));
                        } else if (typeof(DeltaControl).IsAssignableFrom(exprType))
                        {
                            if (propSym.Name == "scroll")
                            {
                                // Mouse.current.scroll
                                var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                                if (classDecl == null) return;
                                Type objectType = FindType(_currentModel.GetDeclaredSymbol(classDecl));
                                if (!typeof(MonoBehaviour).IsAssignableFrom(objectType))
                                {
                                    AddAnalysisWarning(node, "Inputs handled outside of a MonoBehaviour are not supported");
                                    return;
                                }
                                string[] path = { objectType.FullName, $"Mouse.current.scroll" };
                                AddAction(new MouseScrollAction(path, objectType));
                            }
                        }
                    }
                } else if (type == typeof(UnityEngine.InputSystem.Pointer))
                {
                    var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                    if (classDecl == null) return;
                    Type objectType = FindType(_currentModel.GetDeclaredSymbol(classDecl));
                    if (!typeof(MonoBehaviour).IsAssignableFrom(objectType))
                    {
                        AddAnalysisWarning(node, "Inputs handled outside of a MonoBehaviour are not supported");
                        return;
                    }

                    string[] path = { objectType.FullName, $"Mouse.current.{propSym.Name}" };
                    switch (propSym.Name)
                    {
                        case "position":
                        case "delta":
                        {
                            AddAction(new MousePositionAction(path, objectType));
                            break;
                        }
                    }
                }
            }
            
            base.VisitMemberAccessExpression(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl == null) return;
            Type objectType = FindType(_currentModel.GetDeclaredSymbol(classDecl));
            if (typeof(MonoBehaviour).IsAssignableFrom(objectType))
            {
                var declSym = _currentModel.GetDeclaredSymbol(node);
                if (declSym.Parameters.Length == 0)
                {
                    switch (declSym.Name)
                    {
                        // OnMouseOver(), OnMouseEnter(), OnMouseExit() handler
                        case "OnMouseOver":
                        case "OnMouseEnter":
                        case "OnMouseExit":
                        {
                            string[] path = { objectType.FullName, declSym.Name };
                            AddAction(new MouseHoverObjectAction(path, objectType));
                            break;
                        }
                        
                        // OnMouseDown(), OnMouseUp(), OnMouseUpAsButton(), OnMouseDrag() handler
                        case "OnMouseDown":
                        case "OnMouseUp":
                        case "OnMouseUpAsButton":
                        case "OnMouseDrag":
                        {
                            string[] path = { objectType.FullName, declSym.Name };
                            AddAction(new MousePressObjectAction(path, objectType));
                            break;
                        }
                    }
                }
            }
            
            base.VisitMethodDeclaration(node);
        }

        private void AddAnalysisWarning(SyntaxNode node, string message)
        {
            Warnings.Add(new RGActionAnalysisWarning(node, message));
        }

        private void AddAction(RGGameAction action)
        {
            string path = string.Join("/", action.Paths[0]);
            string currentPath = path;
            int count = 1;
            while (_rawActions.ContainsKey(currentPath))
            {
                ++count;
                currentPath = path + count;
            }
            action.Paths[0] = currentPath.Split("/");
            _rawActions.Add(currentPath, action);
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
        
        private void RunCodeAnalysis()
        {
            const float codeAnalysisStartProgress = 0.0f;
            const float codeAnalysisEndProgress = 0.6f;
            NotifyProgress("Performing code analysis", codeAnalysisStartProgress);
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
                    NotifyProgress($"Analyzing {asm.name}", progress);
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

        private void AnalyzeGameObject(GameObject gameObject)
        {
            if (gameObject.TryGetComponent(out Button btn) && !IsRGOverlayObject(gameObject))
            {
                foreach (string listener in RGActionManagerUtils.GetEventListenerMethodNames(btn.onClick))
                {
                    _buttonClickListeners.Add(listener);
                }
            }
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
                int analyzedResourceCount = 0;
                int totalResourceCount = sceneGuids.Length + prefabGuids.Length;
                foreach (string sceneGuid in sceneGuids)
                {
                    float progress = Mathf.Lerp(resourceAnalysisStartProgress, resourceAnalysisEndProgress,
                        analyzedResourceCount / (float)totalResourceCount);
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
                    ++analyzedResourceCount;
                }

                foreach (string prefabGuid in prefabGuids)
                {
                    float progress = Mathf.Lerp(resourceAnalysisStartProgress, resourceAnalysisEndProgress,
                        analyzedResourceCount / (float)totalResourceCount);
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
                    ++analyzedResourceCount;
                }
                
                foreach (string btnClickListener in _buttonClickListeners)
                {
                    string[] path = {"Unity UI", "Button Click", btnClickListener};
                    AddAction(new UIButtonPressAction(path, typeof(Button), btnClickListener));
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
                Warnings = new List<RGActionAnalysisWarning>();
            
                RunCodeAnalysis();
                RunResourceAnalysis();
            
                NotifyProgress("Saving analysis results", 0.9f);

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