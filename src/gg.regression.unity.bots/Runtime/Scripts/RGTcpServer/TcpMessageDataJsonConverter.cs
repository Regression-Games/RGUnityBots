using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.BotCriteria;

namespace RegressionGames
{
    public sealed class TcpMessageDataJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(TcpMessage).IsAssignableFrom(objectType);
        }
        
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            TcpMessage message = new();
           
            message.type = jObject["type"].ToObject<TcpMessageType>(serializer);
            ITcpMessageData payload = null;
            switch (message.type)
            {
                case TcpMessageType.PING:
                case TcpMessageType.PONG:
                case TcpMessageType.CLOSE:
                case TcpMessageType.APPLICATION_QUIT:
                    // these messages have no payload
                    break;
                case TcpMessageType.ACTIVE_SEQUENCE:
                    payload = jObject["payload"].ToObject<ActiveSequenceTcpMessageData>(serializer);
                    break;
                case TcpMessageType.AVAILABLE_SEQUENCES:
                    payload = jObject["payload"].ToObject<AvailableSequencesTcpMessageData>(serializer);
                    break;
                case TcpMessageType.PLAY_SEQUENCE:
                    payload = jObject["payload"].ToObject<PlaySequenceTcpMessageData>(serializer);
                    break;
                default:
                    throw new JsonSerializationException($"Unsupported TcpMessage type: '{message.type}'");
            }

            message.payload = payload;
            return message;
        }
        
        public override bool CanWrite => false;
        
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}