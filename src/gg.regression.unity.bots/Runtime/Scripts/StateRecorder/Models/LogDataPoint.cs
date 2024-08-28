using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    public class LogDataPoint
    {
        public DateTimeOffset timestamp { get; }
        
        // used to correlate logs to recorded tick data
        public double frameTimestamp { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public LogType level { get; }
        public string message { get; }
        public string stackTrace { get; }
        
        public LogDataPoint(DateTimeOffset timestamp, double frameTimestamp, LogType level, string message, string stackTrace)
        {
            this.timestamp = timestamp;
            this.frameTimestamp = frameTimestamp;
            this.level = level;
            this.message = message;
            this.stackTrace = stackTrace;
        }
    }
}