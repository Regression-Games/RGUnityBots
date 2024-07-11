using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class PerformanceMetricData: IComponentDataProvider
    {
        public int apiVersion = SdkApiVersion.VERSION_4;

        public int ApiVersion()
        {
            return apiVersion;
        }

        public int EffectiveApiVersion => Math.Max(apiVersion, perFrameStatistics.DefaultIfEmpty().Max(a => a?.apiVersion ?? 0));

        public double previousTickTime;
        public int framesSincePreviousTick;
        public int fps;
        public List<PerFrameStatisticsData> perFrameStatistics;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"previousTickTime\":");
            DoubleJsonConverter.WriteToStringBuilder(stringBuilder, previousTickTime);
            stringBuilder.Append(",\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"framesSincePreviousTick\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, framesSincePreviousTick);
            stringBuilder.Append(",\n\"fps\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, fps);
            stringBuilder.Append(",\n\"perFrameStatistics\":[\n");
            int perFrameStatisticsCount = perFrameStatistics.Count;
            for (int i = 0; i < perFrameStatisticsCount; ++i)
            {
                perFrameStatistics[i].WriteToStringBuilder(stringBuilder);
                if (i + 1 < perFrameStatisticsCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]\n}");
        }
    }
}
