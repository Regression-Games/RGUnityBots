using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.CVSerice;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public sealed class CVImageEncodedDataJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(CVImageEncodedData).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {

            JObject jObject = JObject.Load(reader);
            // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug when on separate lines
            CVImageEncodedData actionModel = new();
            actionModel.data = jObject.GetValue("data").ToObject<string>(serializer);
            actionModel.width = jObject.GetValue("width").ToObject<int>(serializer);
            actionModel.height = jObject.GetValue("height").ToObject<int>(serializer);
            return actionModel;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
