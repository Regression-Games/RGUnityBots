using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models.BotActions;
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
            if (jObject.ContainsKey("apiVersion"))
            {
                data.apiVersion = jObject.GetValue("apiVersion").ToObject<int>(serializer);
            }

            data.allowDrag = jObject.GetValue("allowDrag").ToObject<bool>(serializer);
            data.timeBetweenClicks = jObject.GetValue("timeBetweenClicks").ToObject<float>(serializer);
            data.excludedAreas = jObject.GetValue("excludedAreas").ToObject<List<RectInt>>(serializer);
            data.excludedNormalizedPaths = jObject.GetValue("excludedNormalizedPaths").ToObject<List<string>>(serializer);
            data.preconditionNormalizedPaths = jObject.GetValue("preconditionNormalizedPaths").ToObject<List<string>>(serializer);
            return data;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

}
