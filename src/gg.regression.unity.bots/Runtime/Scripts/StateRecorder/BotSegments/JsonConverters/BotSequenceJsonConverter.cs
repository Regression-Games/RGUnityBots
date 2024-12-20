using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public sealed class BotSequenceJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(BotSequence).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug when on separate lines
            BotSequence sequence = new();
            sequence.name = jObject.GetValue("name").ToObject<string>(serializer);
            sequence.description = jObject.GetValue("description")?.ToObject<string>(serializer) ?? "";
            sequence.segments = jObject.GetValue("segments").ToObject<List<BotSequenceEntry>>(serializer);
            sequence.validations = jObject.GetValue("validations")?.ToObject<List<SegmentValidation>>(serializer) ?? new List<SegmentValidation>();
            return sequence;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
