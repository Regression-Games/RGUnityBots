#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using Mono.Cecil.Rocks;
using RegressionGames.CodeCoverage;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using Assembly = UnityEditor.Compilation.Assembly;

using RegressionGames.StateRecorder.BotSegments;

#if ENABLE_LEGACY_INPUT_MANAGER
using RegressionGames.RGLegacyInputUtility;
#endif

namespace RegressionGames.Editor
{
    /*
     * This class is responsible for hooking into the Unity build process and
     * applying instrumentation for wrapper input API instrumentation and code coverage recording.
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

        private static bool IsAssemblyIgnored(Assembly assembly, Assembly rgAssembly)
        {
            if (assembly == null)
            {
                return true; // not a player assembly
            }

            if (assembly.sourceFiles.Length == 0)
            {
                // don't instrument any assemblies we don't have the source code for
                return true;
            }

            foreach (var sourceFile in assembly.sourceFiles)
            {
                if (sourceFile.StartsWith("Packages/"))
                {
                    // ignore packages
                    return true;
                }
            }

            // ignore plugin packages referenced by the RG package
            if (rgAssembly != null)
            {
                string fileName = Path.GetFileName(assembly.outputPath);
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

        private static string GetSubsignature(MethodReference method)
        {
            return method.Name + "(" + string.Join(",", method.Parameters.Select(param => param.ParameterType.FullName)) + ")";
        }

        private static ModuleDefinition ReadRGRuntimeAssembly()
        {
            string assemblyPath = Path.GetFullPath(typeof(RGCodeCoverage).Assembly.Location);
            return ModuleDefinition.ReadModule(assemblyPath, new ReaderParameters()
            {
                AssemblyResolver = _assemblyResolver
            });
        }

        #if ENABLE_LEGACY_INPUT_MANAGER
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

        // Returns whether any changes were made
        private static bool ApplyLegacyInputInstrumentation(ModuleDefinition module, Dictionary<string, MethodReference> wrapperMethods)
        {
            if (wrapperMethods.Count == 0)
            {
                return false;
            }
            bool anyChanges = false;
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
                        if (inst.OpCode == OpCodes.Call && inst.Operand is MethodReference methodRef
                                                        && methodRef.DeclaringType.FullName == "UnityEngine.Input")
                        {
                            string subsig = GetSubsignature(methodRef);
                            if (wrapperMethods.TryGetValue(subsig, out MethodReference wrapperMethodRef))
                            {
                                inst.Operand = module.ImportReference(wrapperMethodRef);
                                anyChanges = true;
                            }
                        }
                    }
                }
            }
            return anyChanges;
        }
        #endif // ENABLE_LEGACY_INPUT_MANAGER

        public static MethodReference FindCodeCoverageVisitMethod(ModuleDefinition wrapperModule)
        {
            TypeDefinition type = wrapperModule.GetType(typeof(RGCodeCoverage).FullName);
            if (type != null)
            {
                return type.Methods.FirstOrDefault(m => m.Name == "Visit");
            }
            return null;
        }

        private static void SaveCodePointMetadata(string assemblyName, List<CodePointMetadata> codePointMetadata)
        {
            var metadata = RGCodeCoverage.GetMetadata();
            if (metadata == null)
            {
                metadata = new CodeCoverageMetadata();
            }
            metadata.codePointMetadata[assemblyName] = codePointMetadata;
            RGCodeCoverage.SaveMetadata(metadata);
        }

        /// <summary>
        /// Inserts code coverage instrumentation into the game assemblies.
        /// Returns true whether any changes were made
        /// </summary>
        private static bool ApplyCodeCovInstrumentation(ModuleDefinition module, MethodReference visitMethod)
        {
            // Check whether any code coverage instrumentation is already present
            // If so, then no changes are needed
            foreach (TypeDefinition type in module.Types)
            {
                if (IsNamespaceIgnored(type.Namespace))
                    continue;
                foreach (MethodDefinition method in type.Methods)
                {
                    if (method.Body == null)
                        continue;
                    foreach (var inst in method.Body.Instructions)
                    {
                        if (inst.OpCode == OpCodes.Call && inst.Operand is MethodReference methodRef)
                        {
                            if (methodRef.DeclaringType.Name == "RGCodeCoverage")
                            {
                                // code coverage instrumentation is already present
                                return false;
                            }
                        }
                    }
                }
            }

            int codePointCounter = 0;
            List<CodePointMetadata> codePointMetadata = new List<CodePointMetadata>();
            string assemblyName = module.Assembly.Name.Name;
            bool anyChanges = false;

            // Go through every method in the game assembly and insert code coverage instrumentation
            // This inserts a call to RGCodeCoverage.Visit() after every program statement that has debug information
            // available for it in the assembly.
            foreach (TypeDefinition type in module.Types)
            {
                if (IsNamespaceIgnored(type.Namespace))
                    continue;
                foreach (MethodDefinition method in type.Methods)
                {
                    if (method.Body == null)
                        continue;

                    string subsig = GetSubsignature(method);
                    var processor = method.Body.GetILProcessor();

                    // Insert calls to RGCodeCoverage.Visit() at every sequence point available in the debug information
                    var debugInfo = method.DebugInformation;
                    if (debugInfo != null)
                    {
                        var seqPts = debugInfo.GetSequencePointMapping();
                        if (seqPts.Count > 0)
                        {
                            anyChanges = true;
                            method.Body.SimplifyMacros(); // Call this before injecting new opcodes to avoid overflow with short-form branches
                            foreach (var entry in seqPts)
                            {
                                var inst = entry.Key;
                                processor.InsertBefore(inst, processor.Create(OpCodes.Ldstr, assemblyName));
                                processor.InsertBefore(inst, processor.Create(OpCodes.Ldc_I4, codePointCounter));
                                processor.InsertBefore(inst,
                                    processor.Create(OpCodes.Call, module.ImportReference(visitMethod)));

                                // Record metadata about the code point to be stored later
                                var seqPt = entry.Value;
                                codePointMetadata.Add(new CodePointMetadata()
                                {
                                    sourceFilePath = seqPt.Document?.Url ?? "",
                                    startLine = seqPt.StartLine == 0xfeefee ? -1 : seqPt.StartLine, // 0xfeefee is a magic number indicating unknown
                                    startCol = seqPt.StartColumn,
                                    endLine = seqPt.EndLine == 0xfeefee ? -1 : seqPt.EndLine,
                                    endCol = seqPt.EndColumn,
                                    typeName = type.FullName,
                                    methodSig = subsig
                                });
                                ++codePointCounter;
                            }
                            method.Body.OptimizeMacros(); // Put back short-form branches where possible again
                        }

                    }
                }
            }

            SaveCodePointMetadata(assemblyName, codePointMetadata);

            return anyChanges;
        }

        private static void InstrumentAssemblyIfNeeded(Assembly assembly, Assembly rgAssembly)
        {
            if (assembly != null)
            {
                var assemblyPath = assembly.outputPath;
                if (!File.Exists(assemblyPath) || IsAssemblyIgnored(assembly, rgAssembly))
                {
                    return;
                }

                RGSettings rgSettings = RGSettings.GetOrCreateSettings();
                bool codeCovEnabled = rgSettings.GetFeatureCodeCoverage();
                if (BuildPipeline.isBuildingPlayer && !EditorUserBuildSettings.development)
                {
                    // disable code coverage instrumentation for production builds
                    codeCovEnabled = false;
                }

                bool anyChanges = false;
                string tmpOutputPath = assemblyPath + ".tmp.dll";

                if (_assemblyResolver == null)
                {
                    ResetAssemblyResolver(null);
                }

                using (ModuleDefinition module = ReadAssembly(assemblyPath))
                using (ModuleDefinition wrapperModule = ReadRGRuntimeAssembly())
                {
#if ENABLE_LEGACY_INPUT_MANAGER
                    {
                        var wrapperMethods = FindWrapperMethods(wrapperModule);
                        if (ApplyLegacyInputInstrumentation(module, wrapperMethods))
                        {
                            anyChanges = true;
                            RGDebug.LogInfo($"Instrumented legacy input API usage in assembly: {assemblyPath}");
                        }
                    }
#endif

                    if (codeCovEnabled)
                    {
                        var visitMethod = FindCodeCoverageVisitMethod(wrapperModule);
                        if (visitMethod != null)
                        {
                            if (ApplyCodeCovInstrumentation(module, visitMethod))
                            {
                                anyChanges = true;
                            }
                        }
                        else
                        {
                            RGDebug.LogError($"Failed to find RGCodeCoverage.Visit method, code coverage instrumentation not applied");
                        }
                    }

                    if (anyChanges)
                    {
                        module.Write(tmpOutputPath, new WriterParameters()
                        {
                            WriteSymbols = true,
                            SymbolWriterProvider = new PdbWriterProvider()
                        });
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
        }

        private static readonly string RgAsmName = Path.GetFileName(typeof(BotSegmentsPlaybackController).Assembly.Location);

        private static Assembly GetAssemblyForPath(string assemblyPath, out Assembly rgAssembly)
        {
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);



            var asmPathname = Path.GetFileName(assemblyPath);

            Assembly result = null;
            rgAssembly = null;
            foreach (Assembly assembly in assemblies)
            {
                var pathname = Path.GetFileName(assembly.outputPath);
                if (pathname == RgAsmName)
                {
                    rgAssembly = assembly;
                    if (result != null)
                    {
                        return result;
                    }
                }
                else if (pathname == asmPathname)
                {
                    result = assembly;
                    if (rgAssembly != null)
                    {
                        return result;
                    }
                }
            }
            return result;
        }

        private static void OnAssemblyCompiled(string assemblyAssetPath, CompilerMessage[] messages)
        {
            try
            {
                var assembly = GetAssemblyForPath(assemblyAssetPath, out var rgAssembly);
                InstrumentAssemblyIfNeeded(assembly, rgAssembly);
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
                Assembly rgAssembly = null;
                foreach (Assembly assembly in assemblies)
                {
                    var pathname = Path.GetFileName(assembly.outputPath);
                    if (pathname == RgAsmName)
                    {
                        rgAssembly = assembly;
                        break;
                    }
                }

                foreach (Assembly assembly in assemblies)
                {
                    InstrumentAssemblyIfNeeded(assembly, rgAssembly);
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

    /// <summary>
    /// Processor for Player build events.
    ///
    /// Currently this is just used to clear the existing standalone code coverage metadata
    /// when a new player build is started.
    /// </summary>
    public class RGInstrumentationBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // clear existing code coverage metadata before starting a Player build
            RGCodeCoverage.ClearMetadata();
        }
    }
}
#endif
