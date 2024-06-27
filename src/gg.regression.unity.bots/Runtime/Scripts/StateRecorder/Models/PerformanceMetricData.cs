using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class PerformanceMetricData
    {
        public double previousTickTime;
        public int framesSincePreviousTick;
        public int fps;
        public List<long> cpuTimesPerFrame;
        public List<long> memoryPerFrame;
        public List<long> gcMemoryPerFrame;
        public EngineStatsData engineStats;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"previousTickTime\":");
            DoubleJsonConverter.WriteToStringBuilder(stringBuilder, previousTickTime);
            stringBuilder.Append(",\"framesSincePreviousTick\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, framesSincePreviousTick);
            stringBuilder.Append(",\"fps\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, fps);
            stringBuilder.Append(",\n\"cpuTimesPerFrame\":[");
            for (int i = 0, n = cpuTimesPerFrame.Count; i < n; ++i)
            {
                LongJsonConverter.WriteToStringBuilderNullable(stringBuilder, cpuTimesPerFrame[i]);
                if (i < n - 1)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("],\n\"memoryPerFrame\":[");
            for (int i = 0, n = memoryPerFrame.Count; i < n; ++i)
            {
                LongJsonConverter.WriteToStringBuilderNullable(stringBuilder, memoryPerFrame[i]);
                if (i < n - 1)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("],\n\"gcMemoryPerFrame\":[");
            for (int i = 0, n = gcMemoryPerFrame.Count; i < n; ++i)
            {
                LongJsonConverter.WriteToStringBuilderNullable(stringBuilder, gcMemoryPerFrame[i]);
                if (i < n - 1)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("],\n\"engineStats\":");
            engineStats.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }
    }
}
