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
        //Update me if fields/types change
        public int apiVersion = SdkApiVersion.VERSION_23;

        /// <summary>
        /// Effective API version for this state recording considering all sub elements
        /// </summary>
        public int EffectiveApiVersion => Math.Max(Math.Max(Math.Max(apiVersion, inputs?.EffectiveApiVersion ?? 0), performance.EffectiveApiVersion), state.DefaultIfEmpty().Max(a => a?.EffectiveApiVersion ?? 0));

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
        public List<CameraInfo> cameraInfo;
        public string pixelHash;
        public string currentRenderPipeline;
        public List<string> activeEventSystemInputModules;
        public List<string> activeInputDevices;
        public IEnumerable<RecordedGameObjectState> state;
        public RecordingCodeCoverageState codeCoverage;
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
            stringBuilder.Append(",\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"tickNumber\":");
            LongJsonConverter.WriteToStringBuilder(stringBuilder, tickNumber);
            stringBuilder.Append(",\n\"keyFrame\":[");
            var keyFrameLength = keyFrame.Length;
            for (var i = 0; i < keyFrameLength; i++)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder,keyFrame[i].ToString());
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
            Vector2IntJsonConverter.WriteToStringBuilder(stringBuilder, screenSize);
            stringBuilder.Append(",\n\"cameraInfo\":[");
            var cameraInfoCount = cameraInfo.Count;
            for (var i = 0; i < cameraInfoCount; i++)
            {
                cameraInfo[i].WriteToStringBuilder(stringBuilder);
                if (i + 1 < cameraInfoCount)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("],\n\"performance\":");
            performance.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append(",\n\"pixelHash\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, pixelHash);
            stringBuilder.Append(",\n\"currentRenderPipeline\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, currentRenderPipeline);
            stringBuilder.Append(",\n\"activeEventSystemInputModules\":[");
            var activeEventSystemInputModulesCount = activeEventSystemInputModules.Count;
            for (var i = 0; i < activeEventSystemInputModulesCount; i++)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, activeEventSystemInputModules[i]);
                if (i + 1 < activeEventSystemInputModulesCount)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("],\n\"activeInputDevices\":[");
            var activeInputDevicesCount = activeInputDevices.Count;
            for (var i = 0; i < activeInputDevicesCount; i++)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, activeInputDevices[i]);
                if (i + 1 < activeInputDevicesCount)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("],\n\"state\":[\n");
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
            stringBuilder.Append("\n],\n\"codeCoverage\":");
            if (codeCoverage != null)
            {
                codeCoverage.WriteToStringBuilder(stringBuilder);
            }
            else
            {
                stringBuilder.Append("null");
            }
            stringBuilder.Append(",\n\"inputs\":");
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
