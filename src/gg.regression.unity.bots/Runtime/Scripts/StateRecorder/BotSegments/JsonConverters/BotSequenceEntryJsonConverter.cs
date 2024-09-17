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
            BotSequenceEntry sequence = new();
            sequence.path = jObject.GetValue("path").ToObject<string>(serializer);
            if (jObject.TryGetValue("type", out var type))
            {
                sequence.type = type.ToObject<BotSequenceEntryType>(serializer);
            }
            else
            {
                // dynamically find out the type if not in the file
                var result = BotSequence.LoadBotSegmentOrBotSegmentListFromPath(sequence.path);
                if (result is BotSegmentList)
                {
                    sequence.type = BotSequenceEntryType.SegmentList;
                }
                else
                {
                    sequence.type = BotSequenceEntryType.Segment;
                }
            }

            return sequence;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
