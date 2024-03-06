using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class QuaternionJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (Quaternion)value;
                writer.WriteStartObject();
                writer.WritePropertyName("x");
                writer.WriteValue(val.x);
                writer.WritePropertyName("y");
                writer.WriteValue(val.y);
                writer.WritePropertyName("z");
                writer.WriteValue(val.z);
                writer.WritePropertyName("w");
                writer.WriteValue(val.w);
                writer.WriteEndObject();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Quaternion) || objectType == typeof(Quaternion?);
        }
    }
}
