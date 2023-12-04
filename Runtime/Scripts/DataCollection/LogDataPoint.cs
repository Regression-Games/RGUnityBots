using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace RegressionGames.DataCollection
{
    [Serializable]
    public class LogDataPoint
    {
        public DateTimeOffset timestamp { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public LogType level { get; }

        public string message { get; }
        public string stackTrace { get; }

        public LogDataPoint(
            DateTimeOffset timestamp,
            LogType level,
            string message,
            string stackTrace)
        {
            this.timestamp = timestamp;
            this.level = level;
            this.message = message;
            this.stackTrace = stackTrace;
        }
    }
}
