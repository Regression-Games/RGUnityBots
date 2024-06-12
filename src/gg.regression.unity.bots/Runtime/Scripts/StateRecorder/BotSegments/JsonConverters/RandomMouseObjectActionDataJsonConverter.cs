using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public sealed class RandomMouseObjectActionDataJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(RandomMouseObjectActionData).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            RandomMouseObjectActionData data = new();
            data.screenSize = jObject.GetValue("screenSize").ToObject<Vector2Int>(serializer);
            data.apiVersion = jObject.GetValue("apiVersion").ToObject<int>();
            data.timeBetweenClicks = jObject.GetValue("timeBetweenClicks").ToObject<float>();
            data.excludedAreas = jObject.GetValue("excludedAreas").ToObject<List<RectInt>>();
            data.excludedNormalizedPaths = jObject.GetValue("excludedNormalizedPaths").ToObject<List<string>>();
            return data;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

}
