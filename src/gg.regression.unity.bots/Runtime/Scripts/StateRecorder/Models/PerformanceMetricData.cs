using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class PerformanceMetricData: IComponentDataProvider
    {
        public double previousTickTime;
        public int framesSincePreviousTick;
        public int fps;
        public long? cpuTimeSincePreviousTick;
        public long? memory;
        public long? gcMemory;
        public EngineStatsData engineStats;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"previousTickTime\":");
            DoubleJsonConverter.WriteToStringBuilder(stringBuilder, previousTickTime);
            stringBuilder.Append(",\"framesSincePreviousTick\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, framesSincePreviousTick);
            stringBuilder.Append(",\"fps\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, fps);
            stringBuilder.Append(",\"cpuTimeSincePreviousTick\":");
            LongJsonConverter.WriteToStringBuilderNullable(stringBuilder, cpuTimeSincePreviousTick);
            stringBuilder.Append(",\"memory\":");
            LongJsonConverter.WriteToStringBuilderNullable(stringBuilder, memory);
            stringBuilder.Append(",\"gcMemory\":");
            LongJsonConverter.WriteToStringBuilderNullable(stringBuilder, gcMemory);
            stringBuilder.Append(",\"engineStats\":");
            engineStats.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }
    }
}
