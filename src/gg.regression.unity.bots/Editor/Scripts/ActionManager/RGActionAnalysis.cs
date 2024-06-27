#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
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
        private SemanticModel _currentModel;

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
                            bool matched = false;
                            var keyArg = node.ArgumentList.Arguments[0];
                            var keyArgSym = _currentModel.GetSymbolInfo(keyArg.Expression).Symbol;
                            if (keyArgSym != null)
                            {
                                if (keyArgSym is ILocalSymbol localSym)
                                {
                                    // TODO data-flow analysis
                                } else if (keyArgSym is IFieldSymbol fieldSym)
                                {
                                    if (fieldSym.IsStatic && fieldSym.ContainingType?.TypeKind == TypeKind.Enum)
                                    {
                                        // constant enum Input.GetKey(KeyCode.<key>)
                                        matched = true;
                                        // TODO create LegacyKeyAction
                                    }
                                    else
                                    {
                                        // TODO generate expression for field
                                    }
                                } 
                            }
                            else if (keyArg.Expression is LiteralExpressionSyntax literalExpr)
                            {
                                var literalKind = literalExpr.Kind();
                                if (literalKind == SyntaxKind.StringLiteralExpression)
                                {
                                    // constant string Input.GetKey("<key name>")
                                    matched = true;
                                    // TODO create LegacyKeyAction
                                }
                            }
                            if (!matched)
                            {
                                AddAnalysisWarning(node, "Could not identify key code being used");
                            }
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
                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    _currentModel = compilation.GetSemanticModel(syntaxTree);
                    var root = syntaxTree.GetCompilationUnitRoot();
                    Visit(root);
                }
            }
        }
    }
}
#endif