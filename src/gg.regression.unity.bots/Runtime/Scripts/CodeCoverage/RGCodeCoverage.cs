using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace RegressionGames.CodeCoverage
{
    /// <summary>
    /// Class responsible for tracking code coverage.
    /// </summary>
    public static class RGCodeCoverage
    {
        // Indicates whether to use the standalone metadata or the editor build metadata
        private static bool IsStandalone
        {
            get
            {
        		bool isStandalone = false;
        		#if UNITY_EDITOR
        		isStandalone = UnityEditor.BuildPipeline.isBuildingPlayer;
        		#else
        		isStandalone = true;
        		#endif
                return isStandalone;
            }
        }
        
        public static string CodeCovMetadataJsonName => IsStandalone ? "RGCodeCoverageStandaloneMetadata" : "RGCodeCoverageMetadata";
        public static string CodeCovMetadataJsonPath => $"Assets/Resources/{CodeCovMetadataJsonName}.txt";
        
        /// <summary>
        /// Dictionary tracking code coverage in the assemblies.
        /// Maps assembly name to visited code points.
        /// This dictionary should always be locked whenever accessing to ensure thread-safety.
        /// </summary>
        private static Dictionary<string, ISet<int>> _codeCoverage { get; set; } = new Dictionary<string, ISet<int>>();
        
        private static bool _isRecording = false;

        private static CodeCoverageMetadata _metadataEditorCache;
        private static CodeCoverageMetadata _metadataStandaloneCache;

        private static CodeCoverageMetadata MetadataCached
        {
            get => IsStandalone ? _metadataStandaloneCache : _metadataEditorCache;
            set
            {
                if (IsStandalone)
                    _metadataStandaloneCache = value;
                else
                    _metadataEditorCache = value;
            }
        }

        public static CodeCoverageMetadata GetMetadata(bool forceReload = false)
        {
            if (MetadataCached != null && !forceReload)
            {
                return MetadataCached;
            }
            string json;
            #if UNITY_EDITOR
            if (File.Exists(CodeCovMetadataJsonPath))
            {
                json = File.ReadAllText(CodeCovMetadataJsonPath);
            }
            else
            {
                json = null;
            }
            #else
            TextAsset jsonFile = Resources.Load<TextAsset>(CodeCovMetadataJsonName);
            json = jsonFile?.text;
            #endif
            if (json != null)
            {
                MetadataCached = JsonConvert.DeserializeObject<CodeCoverageMetadata>(json);
            }
            if (MetadataCached != null && !MetadataCached.IsValid())
            {
                MetadataCached = null;
            }
            return MetadataCached;
        }

        #if UNITY_EDITOR
        public static void ClearMetadata()
        {
            if (File.Exists(CodeCovMetadataJsonPath))
            {
                File.Delete(CodeCovMetadataJsonPath);
            }

            string metaFilePath = CodeCovMetadataJsonPath + ".meta";
            if (File.Exists(metaFilePath))
            {
                File.Delete(metaFilePath);
            }
            
            MetadataCached = null;
        }
        
        public static void SaveMetadata(CodeCoverageMetadata metadata)
        {
            string targetDir = Path.GetDirectoryName(CodeCovMetadataJsonPath);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            File.WriteAllText(CodeCovMetadataJsonPath, JsonConvert.SerializeObject(metadata, Formatting.Indented));
            MetadataCached = metadata;
        }
        #endif
        
        /// <summary>
        /// Begin recording code coverage
        /// </summary>
        public static void StartRecording()
        {
            Clear();
            _isRecording = true;
        }

        /// <summary>
        /// Stop recording code coverage
        /// </summary>
        public static void StopRecording()
        {
            _isRecording = false;
        }

        /// <summary>
        /// If code coverage is enabled, then a call to this method is inserted at every
        /// statement in the game code by RGInstrumentation.
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public static void Visit(string assemblyName, int codePointId)
        {
            if (_isRecording)
            {
                lock (_codeCoverage)
                {
                    ISet<int> visitedPoints;
                    if (!_codeCoverage.TryGetValue(assemblyName, out visitedPoints))
                    {
                        visitedPoints = new HashSet<int>();
                        _codeCoverage.Add(assemblyName, visitedPoints);
                    }
                    visitedPoints.Add(codePointId);
                }
            }
        }

        /// <summary>
        /// Copies the current set of covered code points as a mapping
        /// from assembly names to code point IDs
        /// </summary>
        public static Dictionary<string, ISet<int>> CopyCodeCoverageState()
        {
            var result = new Dictionary<string, ISet<int>>();
            lock (_codeCoverage)
            {
                foreach (var entry in _codeCoverage)
                {
                    result.Add(entry.Key, new HashSet<int>(entry.Value));
                }
            }
            return result;
        }
        
        /// <summary>
        /// Clear any recorded code coverage data up to now
        /// </summary>
        public static void Clear()
        {
            lock (_codeCoverage)
            {
                foreach (var entry in _codeCoverage)
                {
                    entry.Value.Clear();
                }
            }
        }
    }
}