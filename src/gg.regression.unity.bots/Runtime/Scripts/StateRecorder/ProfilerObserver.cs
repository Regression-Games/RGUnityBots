using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace RegressionGames.StateRecorder
{
    public struct ProfilerObserverResult
    {
        // Amount of memory (in bytes) the operating system reports in use by the application on each frame since the last tick
        public List<long> systemUsedMemoryPerFrame;

        // Used heap size (in bytes) that is garbage collected on each frame since the last tick
        public List<long> gcUsedMemoryPerFrame;

        // Time spent (in nanoseconds) by the CPU on the main thread on each frame since the last tick
        public List<long> cpuTimePerFrame;

        public void Clear()
        {
            systemUsedMemoryPerFrame.Clear();
            gcUsedMemoryPerFrame.Clear();
            cpuTimePerFrame.Clear();
        }
    }

    public class ProfilerObserver : MonoBehaviour
    {
        private const int MAX_FRAMES_ACCUM = 16384;

        private ProfilerRecorder _systemMemoryRecorder;
        private ProfilerRecorder _gcMemoryRecorder;
        private ProfilerRecorder _cpuTimeRecorder;
        private List<ProfilerRecorderSample> _sampleBuf;
        private ProfilerObserverResult _resultBuf;

        public void StartProfiling()
        {
            _systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory", MAX_FRAMES_ACCUM);
            _gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory", MAX_FRAMES_ACCUM);
            _cpuTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", MAX_FRAMES_ACCUM);
            _sampleBuf = new List<ProfilerRecorderSample>(MAX_FRAMES_ACCUM);
            _resultBuf = new ProfilerObserverResult();
            _resultBuf.systemUsedMemoryPerFrame = new List<long>(MAX_FRAMES_ACCUM);
            _resultBuf.gcUsedMemoryPerFrame = new List<long>(MAX_FRAMES_ACCUM);
            _resultBuf.cpuTimePerFrame = new List<long>(MAX_FRAMES_ACCUM);
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
        /// Reads the last numFrames profiler frames into the output buffer.
        /// </summary>
        private void ReadProfilerValues(ProfilerRecorder recorder, int numFrames, List<long> outputBuf)
        {
            numFrames = Math.Min(numFrames, recorder.Capacity);
            
            _sampleBuf.Clear();
            recorder.CopyTo(_sampleBuf);

            if (recorder.WrappedAround)
            {
                int unwrappedCount = Math.Max(numFrames - recorder.Count, 0);
                for (int i = _sampleBuf.Count - unwrappedCount, n = _sampleBuf.Count; i < n; ++i)
                {
                    outputBuf.Add(_sampleBuf[i].Value);
                }
                for (int i = recorder.Count - (numFrames - unwrappedCount), n = recorder.Count; i < n; ++i)
                {
                    outputBuf.Add(_sampleBuf[i].Value);
                }
            }
            else
            {
                for (int i = Math.Max(recorder.Count - numFrames, 0), n = recorder.Count; i < n; ++i)
                {
                    outputBuf.Add(_sampleBuf[i].Value);
                }
            }
            
            Debug.Assert(outputBuf.Count <= numFrames);
        }

        public ProfilerObserverResult SampleProfiler(int frameCountSinceLastTick)
        {
            _resultBuf.Clear();
            if (_systemMemoryRecorder.Valid)
            {
                ReadProfilerValues(_systemMemoryRecorder, frameCountSinceLastTick, _resultBuf.systemUsedMemoryPerFrame);
            }
            if (_gcMemoryRecorder.Valid)
            {
                ReadProfilerValues(_gcMemoryRecorder, frameCountSinceLastTick, _resultBuf.gcUsedMemoryPerFrame);
            }
            if (_cpuTimeRecorder.Valid)
            {
                ReadProfilerValues(_cpuTimeRecorder, frameCountSinceLastTick, _resultBuf.cpuTimePerFrame);
            }
            return _resultBuf;
        }
    }
}
