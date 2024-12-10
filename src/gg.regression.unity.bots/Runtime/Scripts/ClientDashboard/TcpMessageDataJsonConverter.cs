using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RegressionGames.ClientDashboard
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
                case TcpMessageType.Ping:
                case TcpMessageType.Pong:
                case TcpMessageType.CloseConnection:
                    // these messages have no payload
                    break;
                case TcpMessageType.ActiveSequence:
                    payload = jObject["payload"].ToObject<ActiveSequenceTcpMessageData>(serializer);
                    break;
                case TcpMessageType.AvailableSequences:
                    payload = jObject["payload"].ToObject<AvailableSequencesTcpMessageData>(serializer);
                    break;
                case TcpMessageType.PlaySequence:
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