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
        
        // frameTimestamp dictates when messages were logged within the context of a tick
        // and is mainly used when displaying recorded data to the user.
        // this uses the same measurement as a tick's timestamp in the recording -
        // at time of writing, this measurement is Time.unscaledTimeAsDouble
        // but to be certain, check the tick "time" field.
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