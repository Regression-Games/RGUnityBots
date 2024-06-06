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
    public class RecordingFrameStateData : BaseFrameStateData
    {
        /**
         * <summary>Reference to the original recording this was created from during replay, possibly null</summary>
         */
        public string referenceSessionId = null;

        public PerformanceMetricData performance;
        public new IEnumerable<RecordedGameObjectState> state;

        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(10_000_000);

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"sessionId\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, sessionId);
            stringBuilder.Append(",\n\"referenceSessionId\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, referenceSessionId);
            stringBuilder.Append(",\n\"tickNumber\":");
            LongJsonConverter.WriteToStringBuilder(stringBuilder, tickNumber);
            stringBuilder.Append(",\n\"keyFrame\":[");
            var keyFrameLength = keyFrame.Length;
            for (var i = 0; i < keyFrameLength; i++)
            {
                stringBuilder.Append("\"").Append(keyFrame[i].ToString()).Append("\"");
                if (i + 1 < keyFrameLength)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("],\n\"time\":");
            DoubleJsonConverter.WriteToStringBuilder(stringBuilder, time);
            stringBuilder.Append(",\n\"timeScale\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, timeScale);
            stringBuilder.Append(",\n\"screenSize\":");
            VectorIntJsonConverter.WriteToStringBuilder(stringBuilder, screenSize);
            stringBuilder.Append(",\n\"performance\":");
            performance.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append(",\n\"pixelHash\":\"");
            stringBuilder.Append(pixelHash);
            stringBuilder.Append("\",\n\"state\":[\n");
            var counter = 0;
            var stateCount = state.Count();
            foreach( var stateEntry in state)
            {
                stateEntry.WriteToStringBuilder(stringBuilder);
                if (++counter < stateCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n],\n\"inputs\":");
            inputs.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("\n}");
        }

        public string ToJsonString()
        {
            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder);
            return _stringBuilder.ToString();
        }

    }
}
