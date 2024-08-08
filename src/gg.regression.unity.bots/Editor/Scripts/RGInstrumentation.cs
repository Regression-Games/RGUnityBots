#if ENABLE_LEGACY_INPUT_MANAGER
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
    public class RGInstrumentation
    {
        private const int MaxAttempts = 4;
        
        private static double _scheduledInstrumentationTime;
        private static int _numInstrumentationAttempts;
        
        [InitializeOnLoadMethod]
        static void OnStartup()
        {
            CompilationPipeline.compilationStarted += ResetAssemblyResolver;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            
            _numInstrumentationAttempts = 0;
            _scheduledInstrumentationTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += ScheduledInstrumentationLoop;
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

        private static RGAssemblyResolver _assemblyResolver = null;

        private static void ResetAssemblyResolver(object o)
        {
            _assemblyResolver?.Dispose();
            _assemblyResolver = CreateAssemblyResolver();
        }

        private static RGAssemblyResolver CreateAssemblyResolver()
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

            RGAssemblyResolver resolver = new RGAssemblyResolver();
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
                AssemblyResolver = _assemblyResolver,
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
                AssemblyResolver = _assemblyResolver
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
            if (!File.Exists(assemblyPath) || IsAssemblyIgnored(assemblyPath, rgAssembly))
            {
                return;
            }

            bool anyChanges = false;
            string tmpOutputPath = assemblyPath + ".tmp.dll";

            if (_assemblyResolver == null)
            {
                ResetAssemblyResolver(null);
            }

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
        
        private static void OnAssemblyCompiled(string assemblyAssetPath, CompilerMessage[] messages)
        {
            try
            {
                Assembly rgAssembly = RGEditorUtils.FindRGAssembly();
                InstrumentAssemblyIfNeeded(assemblyAssetPath, rgAssembly);
            }
            catch (Exception e)
            {
                // If the instrumentation failed here, then there is nothing we can do since this could be part of a player build process
                RGDebug.Log($"Instrumentation of legacy input APIs failed for {assemblyAssetPath}. Simulating legacy inputs may not work, try re-building the game assemblies.\n{e.Message}\n{e.StackTrace}");
            }
        }

        static void ScheduledInstrumentationLoop()
        {
            bool editorIsBusy = EditorApplication.isCompiling || EditorApplication.isUpdating;
            
            bool done;
            if (!editorIsBusy && EditorApplication.timeSinceStartup >= _scheduledInstrumentationTime)
            {
                if (InstrumentExistingAssemblies())
                {
                    done = true;
                }
                else
                {
                    // if the instrumentation failed, attempt a couple more times with exponential backoff timeout
                    if (_numInstrumentationAttempts < MaxAttempts-1)
                    {
                        double timeout = 3.0 * Math.Pow(2.0, _numInstrumentationAttempts); // 3 sec, 6 sec, 12 sec
                        _scheduledInstrumentationTime = EditorApplication.timeSinceStartup + timeout;
                        ++_numInstrumentationAttempts;
                        done = false;
                    }
                    else
                    {
                        done = true;
                    }
                }
            }
            else
            {
                done = false;
            }
            if (done)
            {
                _numInstrumentationAttempts = 0;
                EditorApplication.update -= ScheduledInstrumentationLoop;
            }
        }
        
        private static bool InstrumentExistingAssemblies()
        {
            try
            {
                Assembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
                Assembly rgAssembly = RGEditorUtils.FindRGAssembly();
                foreach (Assembly assembly in assemblies)
                {
                    InstrumentAssemblyIfNeeded(assembly.outputPath, rgAssembly);
                }
                return true;
            }
            catch (Exception e)
            {
                // If the instrumentation fails, the ScheduledInstrumentationLoop will schedule a re-attempt a couple more times
                if (_numInstrumentationAttempts+1 == MaxAttempts)
                {
                    RGDebug.LogError($"Instrumenting legacy input APIs failed, maximum number of attempts exhausted. Simulating legacy inputs will not work, try re-building the game assemblies.\n{e.Message}\n{e.StackTrace}");
                }
                else
                {
                    RGDebug.LogWarning($"Attempt {_numInstrumentationAttempts+1}/{MaxAttempts} at instrumenting legacy input APIs failed, scheduling re-attempt\n{e.Message}\n{e.StackTrace}");
                }
                return false;
            }
        }
    }
}
#endif
#endif