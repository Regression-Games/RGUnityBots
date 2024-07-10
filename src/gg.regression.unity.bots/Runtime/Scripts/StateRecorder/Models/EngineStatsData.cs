using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class EngineStatsData
    {
        public int apiVersion = SdkApiVersion.VERSION_4;

        public float frameTime;
        public float renderTime;

        public int triangles;
        public int vertices;

        public int setPassCalls;

        public int drawCalls;
        public int dynamicBatchedDrawCalls;
        public int staticBatchedDrawCalls;
        public int instancedBatchedDrawCalls;

        public int batches;
        public int dynamicBatches;
        public int staticBatches;
        public int instancedBatches;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"frameTime\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, frameTime);
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"renderTime\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, renderTime);
            stringBuilder.Append(",\"triangles\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, triangles);
            stringBuilder.Append(",\"vertices\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, vertices);
            stringBuilder.Append(",\"setPassCalls\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, setPassCalls);
            stringBuilder.Append(",\"drawCalls\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, drawCalls);
            stringBuilder.Append(",\"dynamicBatchedDrawCalls\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, dynamicBatchedDrawCalls);
            stringBuilder.Append(",\"staticBatchedDrawCalls\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, staticBatchedDrawCalls);
            stringBuilder.Append(",\"instancedBatchedDrawCalls\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, instancedBatchedDrawCalls);
            stringBuilder.Append(",\"batches\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, batches);
            stringBuilder.Append(",\"dynamicBatches\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, dynamicBatches);
            stringBuilder.Append(",\"staticBatches\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, staticBatches);
            stringBuilder.Append(",\"instancedBatches\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, instancedBatches);
            stringBuilder.Append("}");
        }
    }
}
