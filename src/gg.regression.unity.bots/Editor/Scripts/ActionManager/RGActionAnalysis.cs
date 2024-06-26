#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Compilation;
using UnityEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RegressionGames.Editor.RGLegacyInputUtility;

namespace RegressionGames.ActionManager
{
    public class RGActionAnalysis
    {
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
        private static ISet<string> GetIgnoredAssemblyNames()
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

        private static IEnumerable<Compilation> GetCompilations()
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
                        SyntaxTree tree = CSharpSyntaxTree.ParseText(sr.ReadToEnd());
                        syntaxTrees.Add(tree);
                    }
                }

                yield return CSharpCompilation.Create(playerAsm.name)
                    .AddReferences(references)
                    .AddSyntaxTrees(syntaxTrees);
            }
        }

        public static void RunAnalysis()
        {
            foreach (var compilation in GetCompilations())
            {
                Debug.Log(compilation.AssemblyName);
            }
        }
    }
}
#endif