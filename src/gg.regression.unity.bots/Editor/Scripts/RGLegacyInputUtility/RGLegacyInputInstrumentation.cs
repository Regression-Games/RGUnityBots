#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using RegressionGames.RGLegacyInputUtility;
using UnityEditor.Compilation;
using UnityEngine;
using Assembly = UnityEditor.Compilation.Assembly;

namespace RegressionGames.Editor.RGLegacyInputUtility
{
    public class RGLegacyInputInstrumentation
    {
        public static void Enable()
        {
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            InstrumentExistingAssemblies();
        }

        private static bool IsAssemblyIgnored(string assemblyPath)
        {
            string fileName = Path.GetFileName(assemblyPath);
            return fileName.Contains("RegressionGames") || fileName.StartsWith("UnityEngine.") ||
                   fileName.StartsWith("Unity.");
        }

        private static bool IsNamespaceIgnored(string ns)
        {
            return ns.Contains("RegressionGames");
        }
        
        private static DefaultAssemblyResolver CreateAssemblyResolver()
        {
            ISet<string> compiledSearchDirs = new HashSet<string>();
            Assembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            foreach (Assembly assembly in assemblies)
            {
                compiledSearchDirs.Add(Path.GetDirectoryName(assembly.outputPath));
            }

            ISet<string> precompiledSearchDirs = new HashSet<string>();
            string[] precompiledPaths = CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.All);
            foreach (string path in precompiledPaths)
            {
                precompiledSearchDirs.Add(Path.GetDirectoryName(path));
            }
            
            DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
            foreach (string searchDir in compiledSearchDirs)
            {
                resolver.AddSearchDirectory(searchDir);
            }
            foreach (string searchDir in precompiledSearchDirs)
            {
                resolver.AddSearchDirectory(searchDir);
            }

            return resolver;
        }
        
        private static ModuleDefinition ReadAssembly(string assemblyPath)
        {   
            return ModuleDefinition.ReadModule(assemblyPath, new ReaderParameters()
            {
                AssemblyResolver = CreateAssemblyResolver()
            });
        }
        
        private static ModuleDefinition ReadWrapperAssembly()
        {
            return ModuleDefinition.ReadModule(typeof(RGLegacyInputWrapper).Assembly.Location, new ReaderParameters()
            {
                AssemblyResolver = CreateAssemblyResolver()
            });
        }
        
        private static string GetSubsignature(MethodReference method)
        {
            return method.Name + "(" + string.Join(",", method.Parameters.Select(param => param.ParameterType.FullName)) + ")";
        }

        private static Dictionary<string, MethodReference> FindWrapperMethods(ModuleDefinition wrapperModule)
        {
            var result = new Dictionary<string, MethodReference>();
            TypeDefinition type = wrapperModule.GetType(typeof(RGLegacyInputWrapper).FullName);
            if (type != null)
            {
                foreach (MethodDefinition method in type.Methods)
                {
                    if (method.IsPublic && method.IsStatic)
                    {
                        result.Add(GetSubsignature(method), method);
                    }
                }
            }
            return result;
        }

        // Yields (instruction, wrapper method) pairs indicating the call sites and appropriate wrapper method to use for each
        private static IEnumerable<(Instruction, MethodReference)> FindInstrumentationPoints(ModuleDefinition module,
            Dictionary<string, MethodReference> wrapperMethods)
        {
            if (wrapperMethods.Count == 0)
            {
                yield break;
            }
            foreach (TypeDefinition type in module.Types)
            {
                if (IsNamespaceIgnored(type.Namespace))
                    continue;

                foreach (MethodDefinition method in type.Methods)
                {
                    if (method.Body == null) 
                        continue;

                    foreach (Instruction inst in method.Body.Instructions)
                    {
                        if (inst.OpCode.Name == "call" && inst.Operand is MethodReference methodRef
                                                       && methodRef.DeclaringType.FullName == "UnityEngine.Input")
                        {
                            string subsig = GetSubsignature(methodRef);
                            if (wrapperMethods.TryGetValue(subsig, out MethodReference wrapperMethodRef))
                            {
                                yield return (inst, wrapperMethodRef);
                            }
                        }
                    }
                }
            }
        }
        
        private static void InstrumentAssemblyIfNeeded(string assemblyPath)
        {
            if (IsAssemblyIgnored(assemblyPath))
            {
                return;
            }
            
            bool anyChanges = false;
            string tmpOutputPath = assemblyPath + ".tmp";
            
            using (ModuleDefinition module = ReadAssembly(assemblyPath))
            using (ModuleDefinition wrapperModule = ReadWrapperAssembly())
            {
                var wrapperMethods = FindWrapperMethods(wrapperModule);
                foreach ((Instruction inst, MethodReference wrapperMethodRef) in FindInstrumentationPoints(module, wrapperMethods))
                {
                    inst.Operand = module.ImportReference(wrapperMethodRef);
                    anyChanges = true;
                }
                if (anyChanges)
                {
                    module.Write(tmpOutputPath);
                }
            }
            
            if (anyChanges)
            {
                File.Move(assemblyPath, assemblyPath + ".bak"); // create a backup of the original assembly for debugging purposes
                File.Move(tmpOutputPath, assemblyPath);
                RGDebug.LogInfo($"Instrumented {assemblyPath}");
            }
        }
        
        private static void OnAssemblyCompiled(string assemblyAssetPath, CompilerMessage[] messages)
        {
            InstrumentAssemblyIfNeeded(assemblyAssetPath);
        }
        
        private static void InstrumentExistingAssemblies()
        {
            Assembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            foreach (Assembly assembly in assemblies)
            {
                InstrumentAssemblyIfNeeded(assembly.outputPath);
            }
        }
    }
}
#endif