using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.BotActions;


namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
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
            // ensure this segment HAS an action
            if (jObject.Count > 0)
            {
                BotAction action = new();
                IBotActionData data = null;
                action.type = jObject["type"].ToObject<BotActionType>();
                switch (action.type)
                {
                    case BotActionType.InputPlayback:
                        data = jObject["data"].ToObject<InputPlaybackActionData>(serializer);
                        break;
                    case BotActionType.RandomMouse_ClickPixel:
                        data = jObject["data"].ToObject<RandomMousePixelActionData>(serializer);
                        break;
                    case BotActionType.RandomMouse_ClickObject:
                        data = jObject["data"].ToObject<RandomMouseObjectActionData>(serializer);
                        break;
                    case BotActionType.Behaviour:
                        data = jObject["data"].ToObject<BehaviourActionData>(serializer);
                        break;
                    case BotActionType.MonkeyBot:
                        data = jObject["data"].ToObject<MonkeyBotActionData>(serializer);
                        break;
                    case BotActionType.Mouse_CVImage:
                        data = jObject["data"].ToObject<CVImageMouseActionData>(serializer);
                        break;
                    case BotActionType.Mouse_CVText:
                        data = jObject["data"].ToObject<CVTextMouseActionData>(serializer);
                        break;
                    case BotActionType.Mouse_ObjectDetection:
                        data = jObject["data"].ToObject<CVObjectDetectionMouseActionData>(serializer);
                        break;
                    default:
                        throw new JsonSerializationException($"Unsupported BotAction type: '{action.type}'");
                }
                action.data = data;
                return action;
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
