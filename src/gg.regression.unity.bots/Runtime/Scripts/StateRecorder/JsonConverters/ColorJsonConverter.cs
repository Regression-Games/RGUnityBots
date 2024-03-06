using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{

    // NOTE: This class exists as a performance optimization as JsonConverters list model for JsonSerializerSettings scales very very poorly
    public class ColorJsonConverter : Newtonsoft.Json.JsonConverter
    {

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var val = (Color)value;
            writer.WriteStartObject();
            writer.WritePropertyName("r");
            writer.WriteValue(val.r);
            writer.WritePropertyName("g");
            writer.WriteValue(val.g);
            writer.WritePropertyName("b");
            writer.WriteValue(val.b);
            writer.WritePropertyName("a");
            writer.WriteValue(val.a);
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Color);
        }

        public override bool CanRead => false;

    }
}
