using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.ActionManager;
using RegressionGames.StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public class MonkeyBotActionDataJsonConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            MonkeyBotActionData data = new MonkeyBotActionData();
            if (jObject.ContainsKey("apiVersion"))
            {
                data.apiVersion = jObject["apiVersion"].ToObject<int>();
            }

            data.actionInterval = jObject["actionInterval"].ToObject<float>();
            data.actionSettings = jObject["actionSettings"].ToObject<RGActionManagerSettings>();
            return data;
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(MonkeyBotActionData).IsAssignableFrom(objectType);
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
