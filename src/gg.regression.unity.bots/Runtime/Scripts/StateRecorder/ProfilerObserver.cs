using Unity.Profiling;
using UnityEngine;

namespace StateRecorder
{
    public struct ProfilerObserverResult
    {
        // Amount of memory (in bytes) the operating system reports in use by the application
        public long? systemUsedMemory;

        // Time spent (in nanoseconds) by the CPU on the main thread on the current frame
        public long? cpuTime;
    }

    public class ProfilerObserver : MonoBehaviour
    {
        private ProfilerRecorder _systemMemoryRecorder;
        private ProfilerRecorder _cpuTimeRecorder;

        public void StartProfiling()
        {
            _systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            _cpuTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        }

        public void StopProfiling()
        {
            if (_systemMemoryRecorder.Valid)
            {
                _systemMemoryRecorder.Dispose();
            }
            if (_cpuTimeRecorder.Valid)
            {
                _cpuTimeRecorder.Dispose();
            }
        }

        public ProfilerObserverResult SampleProfiler()
        {
            ProfilerObserverResult result = new ProfilerObserverResult();
            if (_systemMemoryRecorder.Valid)
            {
                result.systemUsedMemory = _systemMemoryRecorder.LastValue;
            }
            if (_cpuTimeRecorder.Valid)
            {
                result.cpuTime = _cpuTimeRecorder.LastValue;
            }
            return result;
        }
    }
}