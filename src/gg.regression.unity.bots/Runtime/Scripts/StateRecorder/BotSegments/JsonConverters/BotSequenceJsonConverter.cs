using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;

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
            sequence.segments = jObject.GetValue("segments").ToObject<List<BotSequenceEntry>>(serializer);
            sequence.name = jObject.GetValue("name").ToObject<string>(serializer);
            return sequence;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
