using System;
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
            BotSegment actionModel = new();
            actionModel.sessionId = jObject.GetValue("sessionId").ToObject<string>(serializer);
            actionModel.apiVersion = jObject.GetValue("apiVersion").ToObject<int>(serializer);
            actionModel.botAction = jObject.GetValue("botAction").ToObject<BotAction>(serializer);
            actionModel.keyFrameCriteria = jObject.GetValue("keyFrameCriteria").ToObject<KeyFrameCriteria[]>(serializer);
            return actionModel;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
