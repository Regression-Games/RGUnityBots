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
        public int apiVersion = SdkApiVersion.VERSION_12;
        public BotSequenceEntryType type;
        public string path;
        public string entryName;
        public Image icon;

        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new (()=>new(100));

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, type.ToString());
            stringBuilder.Append(",\"apiVersion\":");
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
