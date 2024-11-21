using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public class SequenceRestartCheckpointJsonConverter: JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(SequenceRestartCheckpoint).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug lines without this
            SequenceRestartCheckpoint data = new();
            if (jObject.ContainsKey("apiVersion"))
            {
                data.apiVersion = jObject.GetValue("apiVersion").ToObject<int>(serializer);
            }

            data.resourcePath = jObject.GetValue("resourcePath").ToObject<string>(serializer);
            return data;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
