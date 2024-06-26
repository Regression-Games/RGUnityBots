#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Microsoft.CodeAnalysis;
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
        
        private static IEnumerable<string> FindScriptPaths(ISet<string> ignoredAssemblyNames)
        {
            string projectDir = Path.GetDirectoryName(Application.dataPath);
            string[] csprojFiles = Directory.GetFiles(projectDir, "*.csproj");
            
            foreach (string csprojPath in csprojFiles)
            {
                if (ignoredAssemblyNames.Contains(Path.GetFileNameWithoutExtension(csprojPath)))
                {
                    continue;
                }
                
                XElement csproj;
                try
                {
                    csproj = XElement.Load(csprojPath);
                }
                catch (Exception e)
                {
                    RGDebug.LogWarning($"Error when parsing csproj file {csprojPath}: {e.Message}");
                    continue;
                }

                XNamespace ns = csproj.GetDefaultNamespace();
                foreach (var compileEl in csproj.Descendants(ns.GetName("Compile")))
                {
                    var includeAttr = compileEl.Attribute("Include");
                    if (includeAttr != null)
                    {
                        yield return includeAttr.Value;
                    }
                }
            }
        }
        
        public static bool TryRunAnalysis()
        {
            ISet<string> ignoredAssemblyNames = GetIgnoredAssemblyNames();
            if (ignoredAssemblyNames == null)
            {
                return false;
            }
            
            foreach (string scriptPath in FindScriptPaths(ignoredAssemblyNames))
            {
                Debug.Log(scriptPath);
            }

            return true;
        }
    }
}
#endif