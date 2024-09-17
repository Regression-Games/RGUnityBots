using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public sealed class BotSegmentListJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(BotSegmentList).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug when on separate lines
            BotSegmentList list = new();
            // allow name to be null/undefined in the file for backwards compatibility
            list.name = jObject.GetValue("name")?.ToObject<string>(serializer) ?? "";
            // allow description to be null/undefined in the file
            list.description = jObject.GetValue("description")?.ToObject<string>(serializer) ?? "";
            list.segments = jObject.GetValue("segments").ToObject<List<BotSegment>>(serializer);
            return list;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
