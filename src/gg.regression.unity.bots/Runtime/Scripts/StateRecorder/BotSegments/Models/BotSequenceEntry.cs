using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    public enum BotSequenceEntryType
    {
        Segment,
        SegmentList
    }

    [Serializable]
    [JsonConverter(typeof(BotSequenceEntryJsonConverter))]
    public class BotSequenceEntry
    {
        public int apiVersion = SdkApiVersion.VERSION_20;
        public string path;

        // NOT WRITTEN TO JSON - Computed dynamically when loaded from disk or created
        public BotSequenceEntryType type;
        // NOT WRITTEN TO JSON - Populated from the BotSegment/BotSegmentList at file load time
        public string name;
        // NOT WRITTEN TO JSON - Populated from the BotSegment/BotSegmentList at file load time
        public string description;

        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new (()=>new(100));

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"path\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, path);
            stringBuilder.Append("}");
        }

        public string ToJsonString()
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value);
            return _stringBuilder.Value.ToString();
        }
    }
}
