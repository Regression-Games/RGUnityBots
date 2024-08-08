using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.Models
{
    public class RecordingCodeCoverageState
    {
        public int apiVersion = SdkApiVersion.VERSION_12;
        
        public Dictionary<string, ISet<int>> coverageSinceLastTick;
        public Dictionary<string, int> codePointCounts;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"coverageSinceLastTick\":{\n");
            int codeCovEntryCount = coverageSinceLastTick.Count;
            int entryIdx = 0;
            foreach (var entry in coverageSinceLastTick)
            {
                int codePointCount = entry.Value.Count;
                StringJsonConverter.WriteToStringBuilder(stringBuilder, entry.Key);
                stringBuilder.Append(":[");
                int codePointIdx = 0;
                foreach (var codePointId in entry.Value)
                {
                    IntJsonConverter.WriteToStringBuilder(stringBuilder, codePointId);
                    if (codePointIdx + 1 < codePointCount)
                    {
                        stringBuilder.Append(",");
                    }
                    ++codePointIdx;
                }
                stringBuilder.Append("]");
                if (entryIdx + 1 < codeCovEntryCount)
                {
                    stringBuilder.Append(",\n");
                }
                ++entryIdx;
            }
            stringBuilder.Append("\n},\n\"codePointCounts\":{\n");
            int numCounts = codePointCounts.Count;
            entryIdx = 0;
            foreach (var entry in codePointCounts)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, entry.Key);
                stringBuilder.Append(":");
                IntJsonConverter.WriteToStringBuilder(stringBuilder, entry.Value);
                if (entryIdx + 1 < numCounts)
                {
                    stringBuilder.Append(",\n");
                }
                ++entryIdx;
            }
            stringBuilder.Append("\n}\n}");
        }
    }
}