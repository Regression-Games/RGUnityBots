using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StateRecorder.BotSegments.Models;

namespace StateRecorder.BotSegments.JsonConverters
{
    public sealed class BotActionJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(BotAction).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            BotAction action = new();
            action.type = jObject["type"].ToObject<BotActionType>();
            IBotActionData data = null;
            switch (action.type)
            {
                case BotActionType.InputPlayback:
                    data = jObject["data"].ToObject<InputPlaybackActionData>(serializer);
                    break;

                default:
                    throw new JsonSerializationException($"Unsupported BotAction type: '{action.type}'");
            }

            action.data = data;
            return action;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

}
