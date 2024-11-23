using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models.BotActions;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public sealed class InputPlaybackActionDataJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(InputPlaybackActionData).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            InputPlaybackActionData data = new();
            if (jObject.ContainsKey("apiVersion"))
            {
                data.apiVersion = jObject.GetValue("apiVersion").ToObject<int>(serializer);
            }
            data.startTime = jObject.GetValue("startTime").ToObject<float>(serializer);
            data.inputData = jObject.GetValue("inputData").ToObject<InputData>(serializer);
            return data;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

}
