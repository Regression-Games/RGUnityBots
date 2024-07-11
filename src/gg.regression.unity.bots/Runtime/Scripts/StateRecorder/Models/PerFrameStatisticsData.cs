using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class PerFrameStatisticsData
    {
        public int apiVersion = SdkApiVersion.VERSION_4;

        public double frameTime;
        public long? cpuTimeNs;
        public long? memoryBytes;
        public long? gcMemoryBytes;
        public EngineStatsData engineStats;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"frameTime\":");
            DoubleJsonConverter.WriteToStringBuilder(stringBuilder, frameTime);
            stringBuilder.Append(",\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"cpuTimeNs\":");
            LongJsonConverter.WriteToStringBuilderNullable(stringBuilder, cpuTimeNs);
            stringBuilder.Append(",\n\"memoryBytes\":");
            LongJsonConverter.WriteToStringBuilderNullable(stringBuilder, memoryBytes);
            stringBuilder.Append(",\n\"gcMemoryBytes\":");
            LongJsonConverter.WriteToStringBuilderNullable(stringBuilder, gcMemoryBytes);
            stringBuilder.Append(",\n\"engineStats\":");
            engineStats.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("\n}");
        }
    }
}
