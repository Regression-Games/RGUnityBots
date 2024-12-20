﻿using System;
using System.Text;
using System.Threading;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    public enum BotSequenceEntryType
    {
        Segment,
        SegmentList
    }

    [Serializable]
    public class BotSequenceEntry
    {
        public int apiVersion = SdkApiVersion.VERSION_24;
        // filePath (if not null) OR resourcePath
        public string path;

        // NOT WRITTEN TO JSON - Populated at file load time
        public string resourcePath;
        // NOT WRITTEN TO JSON - Computed dynamically when loaded from disk or created
        public BotSequenceEntryType type;
        // NOT WRITTEN TO JSON - Populated from the BotSegment/BotSegmentList at file load time
        public string name;
        // NOT WRITTEN TO JSON - Populated from the BotSegment/BotSegmentList at file load time
        public string description;
        // NOT WRITTEN TO JSON - Populated at file load time
        public bool isOverride;

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
