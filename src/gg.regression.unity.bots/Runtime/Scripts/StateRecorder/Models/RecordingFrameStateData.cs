using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RecordingFrameStateData
    {
        /**
         * <summary>UUID of the session</summary>
         */
        public string sessionId;
        /**
         * <summary>Reference to the original recording this was created from during replay, possibly null</summary>
         */
        public string referenceSessionId;
        public long tickNumber;
        public KeyFrameType[] keyFrame;
        public double time;
        public float timeScale;
        public Vector2Int screenSize;
        public string pixelHash;
        public IEnumerable<RecordedGameObjectState> state;
        public InputData inputs;

        public PerformanceMetricData performance;

        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(10_000_000));

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
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value);
            return _stringBuilder.Value.ToString();
        }

    }
}
