using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace StateRecorder
{
    public struct ProfilerObserverResult
    {
        // Amount of memory (in bytes) the operating system reports in use by the application
        public long? systemUsedMemory;

        // Used heap size (in bytes) that is garbage collected
        public long? gcUsedMemory;

        // Time spent (in milliseconds) by the CPU on the main thread since the last tick
        public double? cpuTimeSincePreviousTick;
    }

    public class ProfilerObserver : MonoBehaviour
    {
        private const int MAX_FRAMES_ACCUM = 16384;

        private ProfilerRecorder _systemMemoryRecorder;
        private ProfilerRecorder _gcMemoryRecorder;
        private ProfilerRecorder _cpuTimeRecorder;
        private List<ProfilerRecorderSample> _cpuTimeSampleBuf;

        public void StartProfiling()
        {
            _systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            _gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            _cpuTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", MAX_FRAMES_ACCUM);
            _cpuTimeSampleBuf = new List<ProfilerRecorderSample>(MAX_FRAMES_ACCUM);
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

        /**
         * Computes the sum of the last N values of the round-robin sample buffer.
         */
        private static double SumOfLastFrames(ProfilerRecorder recorder, List<ProfilerRecorderSample> samples, int numFrames, out int framesRead)
        {
            double sum = 0.0;
            framesRead = 0;
            for (int frameIndex = recorder.Count - 1; frameIndex >= 0 && framesRead < numFrames; --frameIndex)
            {
                sum += samples[frameIndex].Value;
                ++framesRead;
            }
            if (framesRead < numFrames && recorder.WrappedAround)
            {
                for (int frameIndex = samples.Count - 1, lastIndex = recorder.Count;
                     frameIndex >= lastIndex && framesRead < numFrames;
                     --frameIndex)
                {
                    sum += samples[frameIndex].Value;
                    ++framesRead;
                }
            }
            return sum;
        }

        public ProfilerObserverResult SampleProfiler(int frameCountSinceLastTick)
        {
            ProfilerObserverResult result = new ProfilerObserverResult();
            if (_systemMemoryRecorder.Valid && _systemMemoryRecorder.Count > 0)
            {
                result.systemUsedMemory = _systemMemoryRecorder.LastValue;
            }
            if (_gcMemoryRecorder.Valid && _gcMemoryRecorder.Count > 0)
            {
                result.gcUsedMemory = _gcMemoryRecorder.LastValue;
            }
            if (_cpuTimeRecorder.Valid)
            {
                _cpuTimeSampleBuf.Clear();
                _cpuTimeRecorder.CopyTo(_cpuTimeSampleBuf);
                int framesRead;
                double cpuTime = SumOfLastFrames(_cpuTimeRecorder, _cpuTimeSampleBuf, frameCountSinceLastTick,
                    out framesRead);
                if (framesRead == frameCountSinceLastTick) // only report the total cpuTime if there were sufficient frames recorded for the request
                {
                    result.cpuTimeSincePreviousTick = cpuTime * 1e-6; // convert to milliseconds
                }
            }
            return result;
        }
    }
}