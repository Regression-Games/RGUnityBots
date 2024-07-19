using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public sealed class BehaviourActionDataJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(BehaviourActionData).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug lines without this
            BehaviourActionData data = new();
            data.behaviourFullName = jObject.GetValue("behaviourFullName").ToObject<string>(serializer);
            data.apiVersion = jObject.GetValue("apiVersion").ToObject<int>();
            data.maxRuntimeSeconds = jObject.GetValue("maxRuntimeSeconds").ToObject<float>(serializer);
            return data;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

}
