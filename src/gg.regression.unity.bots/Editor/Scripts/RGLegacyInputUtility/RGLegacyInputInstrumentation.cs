#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using RegressionGames.RGLegacyInputUtility;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Assembly = UnityEditor.Compilation.Assembly;

namespace RegressionGames.Editor.RGLegacyInputUtility
{
    /*
     * This class is responsible for hooking into the Unity build process and
     * applying the wrapper input API instrumentation for the legacy input manager.
     */
    public class RGLegacyInputInstrumentation
    {
        [InitializeOnLoadMethod]
        static void OnStartup()
        {
            // Safer to do any initial instrumentation within the editor update loop.
            // Initial experiments with running directly from static initializers
            // caused errors sometimes.
            EditorApplication.update += SetUpHooks;
        }

        static void SetUpHooks()
        {
            EditorApplication.update -= SetUpHooks;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            InstrumentExistingAssemblies();
        }

        private static bool IsAssemblyIgnored(string assemblyPath, Assembly rgAssembly)
        {
            string fileName = Path.GetFileName(assemblyPath);
            if (fileName.StartsWith("UnityEngine.") || fileName.StartsWith("Unity.")) // ignore game engine assemblies 
            {
                return true;
            }

            if (fileName.Contains("RegressionGames")) // ignore RG assemblies
            {
                return true;
            }

            // ignore plugin packages referenced by the RG package
            if (rgAssembly != null)
            {
                foreach (string asmPath in rgAssembly.allReferences)
                {
                    if (Path.GetFileName(asmPath) == fileName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsNamespaceIgnored(string ns)
        {
            return ns.Contains("RegressionGames");
        }
        
        private static DefaultAssemblyResolver CreateAssemblyResolver(string assemblyPath)
        {
            ISet<string> compiledSearchDirs = new HashSet<string>();
            Assembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
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
            assemblyPath = Path.GetFullPath(assemblyPath);
            return ModuleDefinition.ReadModule(assemblyPath, new ReaderParameters()
            {
                ReadingMode = ReadingMode.Immediate,
                AssemblyResolver = CreateAssemblyResolver(assemblyPath),
                InMemory = true,
                ReadSymbols = true,
                SymbolReaderProvider = new PdbReaderProvider()
            });
        }
        
        private static ModuleDefinition ReadWrapperAssembly()
        {
            string assemblyPath = Path.GetFullPath(typeof(RGLegacyInputWrapper).Assembly.Location);
            return ModuleDefinition.ReadModule(assemblyPath, new ReaderParameters()
            {
                AssemblyResolver = CreateAssemblyResolver(assemblyPath)
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
        
        private static void InstrumentAssemblyIfNeeded(string assemblyPath, Assembly rgAssembly)
        {
            try
            {
                if (IsAssemblyIgnored(assemblyPath, rgAssembly))
                {
                    return;
                }
            
                bool anyChanges = false;
                string tmpOutputPath = assemblyPath + ".tmp.dll";
            
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
                        module.Write(tmpOutputPath, new WriterParameters()
                        {
                            WriteSymbols = true,
                            SymbolWriterProvider = new PdbWriterProvider()
                        });
                        RGDebug.LogInfo($"Instrumented legacy input API usage in assembly: {assemblyPath}");
                    }
                }

                if (anyChanges)
                {
                    string pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
                    File.Delete(assemblyPath);
                    if (File.Exists(pdbPath))
                    {
                        File.Delete(pdbPath);
                    }

                    string outPdbPath = Path.ChangeExtension(tmpOutputPath, ".pdb");
                    File.Move(tmpOutputPath, assemblyPath);
                    if (File.Exists(outPdbPath))
                    {
                        File.Move(outPdbPath, pdbPath);
                    }
                }
            }
            catch (Exception e)
            {
                RGDebug.LogException(e, $"Error during legacy input instrumentation for {assemblyPath}");
            }
        }

        private static Assembly FindRGAssembly()
        {
            var rgAsmName = Path.GetFileName(typeof(RGLegacyInputWrapper).Assembly.Location);
            Assembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            foreach (Assembly assembly in assemblies)
            {
                if (Path.GetFileName(assembly.outputPath) == rgAsmName)
                {
                    return assembly;
                }
            }
            return null;
        }
        
        private static void OnAssemblyCompiled(string assemblyAssetPath, CompilerMessage[] messages)
        {
            Assembly rgAssembly = FindRGAssembly();
            InstrumentAssemblyIfNeeded(assemblyAssetPath, rgAssembly);
        }
        
        private static void InstrumentExistingAssemblies()
        {
            Assembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            Assembly rgAssembly = FindRGAssembly();
            foreach (Assembly assembly in assemblies)
            {
                InstrumentAssemblyIfNeeded(assembly.outputPath, rgAssembly);
            }
        }
    }
}
#endif