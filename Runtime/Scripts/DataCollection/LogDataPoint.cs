using System;
using UnityEngine;

namespace RegressionGames.DataCollection
{
    [Serializable]
    public class LogDataPoint
    {
        public string logString;
        public string stackTrace;
        public LogType type;
        public DateTime timestamp;

        public LogDataPoint(string logString, string stackTrace, LogType type)
        {
            timestamp = DateTime.Now;
            this.logString = logString;
            this.stackTrace = stackTrace;
            this.type = type;
        }
    }
}