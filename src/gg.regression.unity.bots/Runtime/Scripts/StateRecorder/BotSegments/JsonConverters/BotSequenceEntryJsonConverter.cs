using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public sealed class BotSequenceEntryJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(BotSequenceEntry).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug when on separate lines
            BotSequenceEntry sequenceEntry = new();
            sequenceEntry.path = jObject.GetValue("path").ToObject<string>(serializer);
            // dynamically find out the type
            var result = BotSequence.LoadBotSegmentOrBotSegmentListFromPath(sequenceEntry.path);
            if (result.Item3 is BotSegmentList bsl)
            {
                sequenceEntry.type = BotSequenceEntryType.SegmentList;
                sequenceEntry.name = bsl.name;
                sequenceEntry.description = bsl.description;
            }
            else
            {
                var botSegment = (BotSegment)result.Item3;
                sequenceEntry.type = BotSequenceEntryType.Segment;
                sequenceEntry.name = botSegment.name;
                sequenceEntry.description = botSegment.description;
            }

            return sequenceEntry;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
