using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public sealed class BotSegmentJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(BotSegment).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug when on separate lines
            BotSegment botSegment = new();
            // allow name to be null/undefined in the file for backward compatibility (added in SdkApiVersion.VERSION_12)
            botSegment.name = jObject.GetValue("name")?.ToObject<string>(serializer) ?? "";
            // allow description to be null/undefined in the file for backward compatibility (added in SdkApiVersion.VERSION_11)
            botSegment.description = jObject.GetValue("description")?.ToObject<string>(serializer) ?? "";
            // allow sessionId to be null/undefined in the file... so manually created segments don't have to generate one
            botSegment.sessionId = jObject.GetValue("sessionId")?.ToObject<string>(serializer) ?? Guid.NewGuid().ToString("n");
            if (jObject.ContainsKey("apiVersion"))
            {
                botSegment.apiVersion = jObject.GetValue("apiVersion").ToObject<int>(serializer);
            }
            botSegment.botAction = jObject.GetValue("botAction")?.ToObject<BotAction>(serializer);
            if (jObject.ContainsKey("keyFrameCriteria"))
            {
                // backwards compatibility before we renamed keyFrameCriteria to endCriteria
                botSegment.endCriteria = new List<KeyFrameCriteria>(jObject.GetValue("keyFrameCriteria").ToObject<KeyFrameCriteria[]>(serializer));
            }
            else if (jObject.ContainsKey("endCriteria"))
            {
                botSegment.endCriteria = new List<KeyFrameCriteria>(jObject.GetValue("endCriteria").ToObject<KeyFrameCriteria[]>(serializer));
            }
            else
            {
                // default value of endCriteria to empty list if not present
                botSegment.endCriteria = new List<KeyFrameCriteria>();
            }

            return botSegment;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
