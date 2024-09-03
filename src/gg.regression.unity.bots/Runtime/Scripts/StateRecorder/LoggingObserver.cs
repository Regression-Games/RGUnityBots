using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder
{
    /// <summary>
    /// Watches for logging messages during recording
    /// </summary>
    public class LoggingObserver : MonoBehaviour
    {
        public long LoggedWarnings { get; private set;  }
        public long LoggedErrors { get; private set; }
        
        private ConcurrentQueue<LogDataPoint> _logQueue = new();

        public void StartCapturingLogs()
        {
            LoggedWarnings = 0;
            LoggedErrors = 0;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
        }
        
        public void StopCapturingLogs()
        {
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
        }
        
        private void OnLogMessageReceived(string message, string stacktrace, LogType type)
        {
            if (type == LogType.Warning)
            {
                LoggedWarnings++;
            } 
            else if (type == LogType.Error || type == LogType.Exception)
            {
                LoggedErrors++;
            }
            
            // PERF: DateTimeOffset.Now is surprisingly costly, use DateTimeOffset.UtcNow unless we have to know the local timezone.
            // It's still good to use a DateTimeOffset here though because it ensures that when we serialize the data,
            // it is clearly marked as a UTC time (with the `Z` suffix)
            var dataPoint = new LogDataPoint(
                timestamp: DateTimeOffset.UtcNow,
                level: type, 
                message: message, 
                stackTrace: stacktrace
            );
            _logQueue.Enqueue(dataPoint);
        }

        /// <summary>
        /// Dequeues all logs that have been captured since the last call to this method.
        /// Concatenates them into a single string in JSONL format.
        /// </summary>
        /// <returns></returns>
        public string DequeueLogs()
        {
            if (_logQueue.IsEmpty)
            {
                return string.Empty;
            }
            
            // Atomically swap the logs queue with an empty queue.
            // Then we can record this queue's logs while new logs continue to accumulate in the new queue
            var logsThisTick = Interlocked.Exchange(ref _logQueue, new());
            
            // Convert the list into JSONL format
            var builder = new StringBuilder();
            foreach (var line in logsThisTick)
            {
                builder.AppendLine(JsonConvert.SerializeObject(line));
            }

            // Combine the JSON strings with newline characters
            return builder.ToString();
        }
        
    }
    
}