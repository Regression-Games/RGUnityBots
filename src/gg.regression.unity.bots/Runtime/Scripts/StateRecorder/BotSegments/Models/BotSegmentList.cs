﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [JsonConverter(typeof(BotSegmentListJsonConverter))]
    [Serializable]
    public class BotSegmentList
    {
        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(10_000));

        // versioning support for bot segment lists in the SDK, the is for this top level schema only
        // update this if this top level schema changes
        public int apiVersion = SdkApiVersion.VERSION_11;

        // the highest apiVersion component included in this json.. used for compatibility checks on replay load
        public int EffectiveApiVersion => Math.Max(apiVersion, segments.DefaultIfEmpty().Max(a=>a?.EffectiveApiVersion ?? 0));

        /**
         * <summary>Title for this bot segment list. Used for naming on the UI.e</summary>
         */
        public string name;

        /**
         * <summary>Description for this bot segment list. Used for naming on the UI.</summary>
         */
        public string description;
        public List<BotSegment> segments = new();

        internal BotSegmentList()
        {
            // used by json converter
        }

        public BotSegmentList(string name, List<BotSegment> botSegments)
        {
            this.name = name;
            foreach (var botSegment in botSegments)
            {
                if (description == null)
                {
                    description = botSegment.description;
                }
                segments.Add(botSegment);
            }
        }


        public string ToJsonString()
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value);
            return _stringBuilder.Value.ToString();
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, name );
            stringBuilder.Append(",\n\"description\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, description );
            stringBuilder.Append(",\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"segments\":[\n");
            var segmentsCount = segments.Count;
            for (var i = 0; i < segmentsCount; i++)
            {
                var segment = segments[i];
                segment.WriteToStringBuilder(stringBuilder);
                if (i + 1 < segmentsCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]}");
        }
    }
}