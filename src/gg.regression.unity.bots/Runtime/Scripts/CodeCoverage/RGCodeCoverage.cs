using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RegressionGames.CodeCoverage
{
    public static class RGCodeCoverage
    {
        private static readonly string _codeCovMetadataJsonPath = "Assets/Resources/RGCodeCoverageMetadata.txt";
        
        public static Dictionary<string, ISet<int>> CodeCoverage { get; set; } = new Dictionary<string, ISet<int>>();
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
            if (File.Exists(_codeCovMetadataJsonPath))
            {
                json = File.ReadAllText(_codeCovMetadataJsonPath);
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
                _metadata = JsonUtility.FromJson<CodeCoverageMetadata>(json);
            }
            return _metadata;
        }

        #if UNITY_EDITOR
        public static void SaveMetadata(CodeCoverageMetadata metadata)
        {
            string targetDir = Path.GetDirectoryName(_codeCovMetadataJsonPath);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            File.WriteAllText(_codeCovMetadataJsonPath, JsonUtility.ToJson(metadata, true));
        }
        #endif
        
        public static void StartRecording()
        {
            Clear();
            _isRecording = true;
        }

        public static void StopRecording()
        {
            _isRecording = false;
        }

        /// <summary>
        /// If code coverage is enabled, then a call to this method is inserted at every
        /// statement in the game code by RGInstrumentation.
        /// </summary>
        public static void Visit(string assemblyName, int codePointId)
        {
            if (_isRecording)
            {
                ISet<int> visitedPoints;
                if (!CodeCoverage.TryGetValue(assemblyName, out visitedPoints))
                {
                    visitedPoints = new HashSet<int>();
                    CodeCoverage.Add(assemblyName, visitedPoints);
                }
                visitedPoints.Add(codePointId);
            }
        }

        public static Dictionary<string, ISet<int>> CopyCodeCoverageState()
        {
            var result = new Dictionary<string, ISet<int>>();
            foreach (var entry in CodeCoverage)
            {
                result.Add(entry.Key, new HashSet<int>(entry.Value));
            }
            return result;
        }
        
        public static void Clear()
        {
            foreach (var entry in CodeCoverage)
            {
                entry.Value.Clear();
            }
        }
    }
}