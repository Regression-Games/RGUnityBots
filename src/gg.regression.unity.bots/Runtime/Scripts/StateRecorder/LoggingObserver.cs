using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        private Task _fileWriteTask;

        public void StartCapturingLogs()
        {
            LoggedWarnings = 0;
            LoggedErrors = 0;
            _fileWriteTask = null;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
        }
        
        public void StopCapturingLogs()
        {
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            
            // wait for any pending write tasks to complete
            if (_fileWriteTask != null)
            {
                _fileWriteTask.Wait();
            }
        }
        
        private void OnLogMessageReceived(string message, string stacktrace, LogType type)
        {
            if (type == LogType.Warning)
            {
                LoggedWarnings++;
            } else if (type == LogType.Error || type == LogType.Exception)
            {
                LoggedErrors++;
            }
            
            // PERF: DateTimeOffset.Now is surprisingly costly, use DateTimeOffset.UtcNow unless we have to know the local timezone.
            // It's still good to use a DateTimeOffset here though because it ensures that when we serialize the data,
            // it is clearly marked as a UTC time (with the `Z` suffix)
            var dataPoint = new LogDataPoint(
                timestamp: DateTimeOffset.UtcNow,
                frameTimestamp: Time.unscaledTimeAsDouble,
                level: type, 
                message: message, 
                stackTrace: stacktrace
            );
            _logQueue.Enqueue(dataPoint);
        }
        
        /// <summary>
        /// Called each tick to flush any available logs to disk.
        /// We may not actually write logs to file every tick:
        /// If there's a running task accessing the destination file, then we'll skip this tick and try again next tick.
        /// </summary>
        public void FlushLogs(string filePath, CancellationToken token)
        {
            if (_fileWriteTask != null && !_fileWriteTask.IsCompleted)
            {
                // task in progress - skip this tick
                return;
            }
            
            if (_logQueue.IsEmpty)
            {
                // nothing to write
                return;
            }
            
            // Atomically swap the logs queue with an empty queue.
            // Then we can record this queue's logs while new logs continue to accumulate in the new queue
            var lines = Interlocked.Exchange(ref _logQueue, new());
            
            // Convert the list into JSONL format
            var builder = new StringBuilder();
            foreach (var line in lines)
            {
                builder.AppendLine(JsonConvert.SerializeObject(line));
            }

            // Combine the JSON strings with newline characters
            var toWrite = builder.ToString();
            _fileWriteTask = File.AppendAllTextAsync(filePath, toWrite, token);
        }
        
    }
    
}