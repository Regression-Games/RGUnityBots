#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;
using UnityEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RegressionGames.Editor.RGLegacyInputUtility;

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
    }
    
    public class RGActionAnalysis : CSharpSyntaxWalker
    {
        private List<RGGameAction> _actions;
        private List<RGActionAnalysisWarning> _warnings;
        private Compilation _currentCompilation;
        private SemanticModel _currentModel;
        private SyntaxTree _currentTree;
        private Dictionary<AssignmentExpressionSyntax, DataFlowAnalysis> _assignmentExprs;
        private Dictionary<LocalDeclarationStatementSyntax, DataFlowAnalysis> _localDeclarationStmts;

        public List<RGGameAction> Actions => _actions;
        public List<RGActionAnalysisWarning> Warnings => _warnings;
        
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

        private IEnumerable<Compilation> GetCompilations()
        {
            ISet<string> ignoredAssemblyNames = GetIgnoredAssemblyNames();
            Assembly[] playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            foreach (var playerAsm in playerAssemblies)
            {
                string playerAsmName = Path.GetFileNameWithoutExtension(playerAsm.outputPath);
                if (ignoredAssemblyNames.Contains(playerAsmName) 
                    || playerAsmName.StartsWith("UnityEngine.") 
                    || playerAsmName.StartsWith("Unity.")
                    || playerAsm.sourceFiles.Length == 0)
                {
                    continue;
                }
                
                List<MetadataReference> references = new List<MetadataReference>();
                foreach (var playerAsmRef in playerAsm.allReferences)
                {
                    references.Add(MetadataReference.CreateFromFile(playerAsmRef));
                }
                
                List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
                foreach (string sourceFile in playerAsm.sourceFiles)
                {
                    using (StreamReader sr = new StreamReader(sourceFile))
                    {
                        SyntaxTree tree = CSharpSyntaxTree.ParseText(sr.ReadToEnd(), path: sourceFile);
                        syntaxTrees.Add(tree);
                    }
                }

                yield return CSharpCompilation.Create(playerAsm.name)
                    .AddReferences(references)
                    .AddSyntaxTrees(syntaxTrees);
            }
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

            yield break;
        }

        private void VisitKeyExpr(ExpressionSyntax keyExpr)
        {
            bool TryMatch(ExpressionSyntax expr)
            {
                Debug.Log($"matching {expr}");
                var symbol = _currentModel.GetSymbolInfo(expr).Symbol;
                if (symbol != null)
                {
                    if (symbol is IFieldSymbol fieldSym)
                    {
                        if (fieldSym.IsStatic && fieldSym.ContainingType?.TypeKind == TypeKind.Enum)
                        {
                            // constant enum Input.GetKey(KeyCode.<key>)
                            // TODO create LegacyKeyAction
                            return true;
                        }
                        else
                        {
                            // TODO generate LegacyKeyAction using expression for field for key
                            return true;
                        }
                    }
                } else if (expr is LiteralExpressionSyntax literalExpr)
                {
                    var literalKind = literalExpr.Kind();
                    if (literalKind == SyntaxKind.StringLiteralExpression)
                    {
                        // constant string Input.GetKey("<key name>")
                        // TODO create LegacyKeyAction
                        return true;
                    }
                }
                return false;
            }

            bool matched = false;
            var keySym = _currentModel.GetSymbolInfo(keyExpr).Symbol;
            if (keySym != null)
            {
                if (keySym is ILocalSymbol localSym)
                {
                    foreach (var valueExpr in FindCandidateValuesForLocalVariable(localSym))
                    {
                        if (TryMatch(valueExpr))
                        {
                            matched = true;
                        }
                    }
                }
                else
                {
                    matched = TryMatch(keyExpr);
                }
            }
            else
            {
                matched = TryMatch(keyExpr);
            }
            if (!matched)
            {
                AddAnalysisWarning(keyExpr, "Could not identify key code being used");
            }
        }
        
        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var nodeSymInfo = _currentModel.GetSymbolInfo(node.Expression);
            if (nodeSymInfo.Symbol is IMethodSymbol methodSymbol)
            {
                var containingTypeName = methodSymbol.ContainingType.ToString();
                if (containingTypeName == "UnityEngine.Input")
                {
                    string methodName = methodSymbol.Name;
                    switch (methodName)
                    {
                        case "GetKey":
                        case "GetKeyDown":
                        case "GetKeyUp":
                        {
                            var keyArg = node.ArgumentList.Arguments[0];
                            VisitKeyExpr(keyArg.Expression);
                            break;
                        }
                    }
                }
            }
        }

        private void AddAnalysisWarning(SyntaxNode node, string message)
        {
            _warnings.Add(new RGActionAnalysisWarning(node, message));
        }

        public void RunAnalysis()
        {
            _actions = new List<RGGameAction>();
            _warnings = new List<RGActionAnalysisWarning>();
            foreach (var compilation in GetCompilations())
            {
                _currentCompilation = compilation;
                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    _currentModel = compilation.GetSemanticModel(syntaxTree);
                    _currentTree = syntaxTree;
                    _assignmentExprs = null;
                    _localDeclarationStmts = null;
                    var root = syntaxTree.GetCompilationUnitRoot();
                    Visit(root);
                }
            }
        }
    }
}
#endif