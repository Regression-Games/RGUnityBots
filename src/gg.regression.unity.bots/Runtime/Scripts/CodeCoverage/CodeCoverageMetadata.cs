using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.CodeCoverage
{
    /// <summary>
    /// Metadata about a single code point
    /// </summary>
    [Serializable]
    public class CodePointMetadata : IStringBuilderWriteable
    {
        public int apiVersion = SdkApiVersion.VERSION_13;

        public string sourceFilePath;
        public int startLine;
        public int startCol;
        public int endLine;
        public int endCol;
        public string typeName;
        public string methodSig;
        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append("\n,\"sourceFilePath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, sourceFilePath);
            stringBuilder.Append("\n,\"startLine\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, startLine);
            stringBuilder.Append("\n,\"startCol\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, startCol);
            stringBuilder.Append("\n,\"endLine\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, endLine);
            stringBuilder.Append("\n,\"endCol\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, endCol);
            stringBuilder.Append("\n,\"typeName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, typeName);
            stringBuilder.Append("\n,\"methodSig\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, methodSig);
            stringBuilder.Append("\n}");
        }
    }

    [Serializable]
    public class CodeCoverageMetadata : IStringBuilderWriteable
    {
        // Increment this whenever breaking changes are made to the metadata format
        public int apiVersion = SdkApiVersion.VERSION_13;

        public int EffectiveApiVersion => Math.Max(apiVersion, codePointMetadata?.Values.Max(a=>a.Max(b=>b.apiVersion)) ?? SdkApiVersion.CURRENT_VERSION);

        /// <summary>
        /// Mapping from assembly name -> list of metadata for each code point
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public Dictionary<string, List<CodePointMetadata>> codePointMetadata = new();

        public bool IsValid()
        {
            return SdkApiVersion.CURRENT_VERSION >= EffectiveApiVersion && codePointMetadata != null;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append("\n,\"codePointMetadata\":{\n");
            var codePointMetadataCount = codePointMetadata.Count;
            var currentIndex = 1;
            foreach (var (key, list) in codePointMetadata)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, key);
                stringBuilder.Append(":[\n");
                var listCount = list.Count;
                for (var i = 0; i < listCount; i++)
                {
                    var listEntry = list[i];
                    listEntry.WriteToStringBuilder(stringBuilder);
                    if (i + 1 < listCount)
                    {
                        stringBuilder.Append(",\n");
                    }
                }
                stringBuilder.Append("\n]");

                if (currentIndex++ < codePointMetadataCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n}\n}");

        }
    }
}
