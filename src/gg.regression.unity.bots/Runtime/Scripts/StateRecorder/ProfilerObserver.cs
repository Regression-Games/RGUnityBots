using System.Collections.Generic;
using RegressionGames.StateRecorder.Models;
using Unity.Profiling;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RegressionGames.StateRecorder
{
    public class ProfilerObserver : MonoBehaviour
    {
        private ProfilerRecorder _systemMemoryRecorder;
        private ProfilerRecorder _gcMemoryRecorder;
        private ProfilerRecorder _cpuTimeRecorder;
        private Queue<PerFrameStatisticsData> _perFrameStatistics;
        private double _lastTime;

        public void StartProfiling()
        {
            _systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            _gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            _cpuTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            _perFrameStatistics = new Queue<PerFrameStatisticsData>();
            _lastTime = Time.unscaledTimeAsDouble;
        }

        public void StopProfiling()
        {
            if (_systemMemoryRecorder.Valid)
            {
                _systemMemoryRecorder.Dispose();
            }
            if (_gcMemoryRecorder.Valid)
            {
                _gcMemoryRecorder.Dispose();
            }
            if (_cpuTimeRecorder.Valid)
            {
                _cpuTimeRecorder.Dispose();
            }
        }

        /// <summary>
        /// Called every frame to read and store profiler values.
        /// </summary>
        public void Observe()
        {
            PerFrameStatisticsData frameData = new PerFrameStatisticsData();
            double time = Time.unscaledTimeAsDouble;
            frameData.frameTime = time - _lastTime;
            if (_cpuTimeRecorder.Valid && _cpuTimeRecorder.Count > 0)
            {
                frameData.cpuTimeNs = _cpuTimeRecorder.LastValue;
            }
            if (_systemMemoryRecorder.Valid && _systemMemoryRecorder.Count > 0)
            {
                frameData.memoryBytes = _systemMemoryRecorder.LastValue;
            }
            if (_gcMemoryRecorder.Valid && _gcMemoryRecorder.Count > 0)
            {
                frameData.gcMemoryBytes = _gcMemoryRecorder.LastValue;
            }
            frameData.engineStats = new EngineStatsData()
            {
                #if UNITY_EDITOR
                frameTime = UnityStats.frameTime,
                renderTime = UnityStats.renderTime,
                triangles = UnityStats.triangles,
                vertices = UnityStats.vertices,
                setPassCalls = UnityStats.setPassCalls,
                drawCalls = UnityStats.drawCalls,
                dynamicBatchedDrawCalls = UnityStats.dynamicBatchedDrawCalls,
                staticBatchedDrawCalls = UnityStats.staticBatchedDrawCalls,
                instancedBatchedDrawCalls = UnityStats.instancedBatchedDrawCalls,
                batches = UnityStats.batches,
                dynamicBatches = UnityStats.dynamicBatches,
                staticBatches = UnityStats.staticBatches,
                instancedBatches = UnityStats.instancedBatches
                #endif
            };
            _perFrameStatistics.Enqueue(frameData);
            _lastTime = time;
        }

        public List<PerFrameStatisticsData> DequeueAll()
        {
            List<PerFrameStatisticsData> result = new List<PerFrameStatisticsData>(_perFrameStatistics.Count);
            while (_perFrameStatistics.TryDequeue(out var frameStats))
            {
                result.Add(frameStats);
            }
            return result;
        }
    }
}
