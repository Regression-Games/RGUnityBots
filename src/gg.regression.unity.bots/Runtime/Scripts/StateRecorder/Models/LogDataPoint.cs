using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    public class LogDataPoint
    {
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new StringBuilder(500));

        public DateTimeOffset timestamp { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public LogType level { get; }
        public string message { get; }
        public string stackTrace { get; }

        public LogDataPoint(DateTimeOffset timestamp, LogType level, string message, string stackTrace)
        {
            this.timestamp = timestamp;
            this.level = level;
            this.message = message;
            this.stackTrace = stackTrace;
        }

        public string ToJsonString()
        {
            var stringBuilder = _stringBuilder.Value;
            stringBuilder.Clear();
            stringBuilder.Append("{\"timestamp\":");
            // gives timestamps in ISO 8601 Z format with milliseconds - "2024-10-29T14:19:39.3795249Z"
            StringJsonConverter.WriteToStringBuilder(stringBuilder, timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
            stringBuilder.Append(",\"level\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, level.ToString());
            stringBuilder.Append(",\"message\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, message);
            stringBuilder.Append(",\"stackTrace\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, stackTrace);
            stringBuilder.Append("}");
            return stringBuilder.ToString();
        }
    }
}
