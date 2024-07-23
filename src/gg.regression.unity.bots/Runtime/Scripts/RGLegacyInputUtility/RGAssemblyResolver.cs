using System.Reflection;
using Mono.Cecil;
using UnityEngine;

namespace RegressionGames.RGLegacyInputUtility
{
    public class RGAssemblyResolver : DefaultAssemblyResolver
    {
        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            // force in-memory storage of resolved assemblies
            if (!parameters.InMemory)
            {
                parameters = new ReaderParameters()
                {
                    ReadingMode = parameters.ReadingMode,
                    InMemory = true,
                    AssemblyResolver = parameters.AssemblyResolver,
                    MetadataResolver = parameters.MetadataResolver,
                    MetadataImporterProvider = parameters.MetadataImporterProvider,
                    ReflectionImporterProvider = parameters.ReflectionImporterProvider,
                    SymbolStream = parameters.SymbolStream,
                    SymbolReaderProvider = parameters.SymbolReaderProvider,
                    ReadSymbols = parameters.ReadSymbols,
                    ThrowIfSymbolsAreNotMatching = parameters.ThrowIfSymbolsAreNotMatching,
                    ReadWrite = parameters.ReadWrite,
                    ApplyWindowsRuntimeProjections = parameters.ApplyWindowsRuntimeProjections
                };
            }
            return base.Resolve(name, parameters);
        }
    }
}