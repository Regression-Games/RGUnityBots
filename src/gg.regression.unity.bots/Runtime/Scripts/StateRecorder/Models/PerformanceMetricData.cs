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
        public List<PerFrameStatisticsData> perFrameStatistics;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"previousTickTime\":");
            DoubleJsonConverter.WriteToStringBuilder(stringBuilder, previousTickTime);
            stringBuilder.Append(",\n\"framesSincePreviousTick\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, framesSincePreviousTick);
            stringBuilder.Append(",\n\"fps\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, fps);
            stringBuilder.Append(",\n\"perFrameStatistics\":[\n");
            for (int i = 0, n = perFrameStatistics.Count; i < n; ++i)
            {
                perFrameStatistics[i].WriteToStringBuilder(stringBuilder);
                if (i < n - 1)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]\n}");
        }
    }
}
