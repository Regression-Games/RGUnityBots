using System;
using System.Collections.Generic;

namespace RegressionGames.CodeCoverage
{
    [Serializable]
    public class CodeCoverageMetadata
    {
        public List<string> assemblyNames = new List<string>();

        public List<int> codePointCounts = new List<int>(); // same order as assemblyNames

        public Dictionary<string, int> GetCodePointCountsAsDictionary()
        {
            var result = new Dictionary<string, int>();
            for (int i = 0; i < assemblyNames.Count; ++i)
            {
                result.Add(assemblyNames[i], codePointCounts[i]);
            }
            return result;
        }
    }
}