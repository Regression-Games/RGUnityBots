using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public sealed class CVObjectDetectionMouseActionDataJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(CVObjectDetectionMouseActionData).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Null)
            {
                JObject jObject = JObject.Load(reader);
                // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug when on separate lines
                CVObjectDetectionMouseActionData actionModel = new();
                actionModel.apiVersion = jObject.GetValue("apiVersion").ToObject<int>(serializer);
                actionModel.imageQuery = jObject.GetValue("imageQuery")?.ToObject<string>(serializer);
                actionModel.textQuery = jObject.GetValue("textQuery")?.ToObject<string>(serializer);
                actionModel.withinRect = jObject.GetValue("withinRect")?.ToObject<CVWithinRect>(serializer);
                actionModel.threshold = jObject.GetValue("threshold")?.ToObject<float?>(serializer);
                actionModel.actions = jObject.GetValue("actions").ToObject<List<CVMouseActionDetails>>(serializer);
                return actionModel;
            }

            return null;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
