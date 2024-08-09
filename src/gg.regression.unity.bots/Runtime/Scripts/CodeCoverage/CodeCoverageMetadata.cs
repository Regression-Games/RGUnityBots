using System;
using System.Collections.Generic;
using RegressionGames.StateRecorder;

namespace RegressionGames.CodeCoverage
{
    /// <summary>
    /// Metadata about a single code point
    /// </summary>
    [Serializable]
    public class CodePointMetadata
    {
        public string sourceFilePath;
        public int startLine;
        public int startCol;
        public int endLine;
        public int endCol;
        public string typeName;
        public string methodSig;
    }
    
    [Serializable]
    public class CodeCoverageMetadata
    {
        // Increment this whenever breaking changes are made to the metadata format
        public const int CURRENT_API_VERSION = SdkApiVersion.VERSION_12;

        public int apiVersion = CURRENT_API_VERSION;
        
        /// <summary>
        /// Mapping from assembly name -> list of metadata for each code point
        /// </summary>
        public Dictionary<string, List<CodePointMetadata>> codePointMetadata = new();

        public bool IsValid()
        {
            return apiVersion == CURRENT_API_VERSION && codePointMetadata != null;
        }
    }
}