﻿using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.CodeCoverage
{
    /// <summary>
    /// Class responsible for tracking code coverage.
    /// </summary>
    public static class RGCodeCoverage
    {
        public static readonly string CodeCovMetadataJsonPath = "Assets/Resources/RGCodeCoverageMetadata.txt";
        
        /// <summary>
        /// Dictionary tracking code coverage in the assemblies.
        /// Maps assembly name to visited code points.
        /// This dictionary should always be locked whenever accessing to ensure thread-safety.
        /// </summary>
        private static Dictionary<string, ISet<int>> _codeCoverage { get; set; } = new Dictionary<string, ISet<int>>();
        
        private static bool _isRecording = false;
        private static CodeCoverageMetadata _metadata = null;

        public static CodeCoverageMetadata GetMetadata(bool forceReload = false)
        {
            if (_metadata != null && !forceReload)
            {
                return _metadata;
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
            TextAsset jsonFile = Resources.Load<TextAsset>("RGCodeCoverageMetadata");
            json = jsonFile?.text;
            #endif
            if (json != null)
            {
                _metadata = JsonConvert.DeserializeObject<CodeCoverageMetadata>(json);
            }
            if (_metadata != null && !_metadata.IsValid())
            {
                _metadata = null;
            }
            return _metadata;
        }

        #if UNITY_EDITOR
        public static void SaveMetadata(CodeCoverageMetadata metadata)
        {
            string targetDir = Path.GetDirectoryName(CodeCovMetadataJsonPath);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            File.WriteAllText(CodeCovMetadataJsonPath, JsonConvert.SerializeObject(metadata, Formatting.Indented));
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