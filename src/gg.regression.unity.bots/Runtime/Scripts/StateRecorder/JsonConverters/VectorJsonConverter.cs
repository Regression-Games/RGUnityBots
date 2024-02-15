using System;
using Newtonsoft.Json;
using UnityEngine;

namespace StateRecorder.JsonConverters
{
    public class VectorJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {   
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (Vector3)value;
                writer.WriteStartArray();
                writer.WriteValue(val.x);
                writer.WriteValue(val.y);
                writer.WriteValue(val.z);
                writer.WriteEndArray();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Vector3) || objectType == typeof(Vector2) || objectType == typeof(Vector3?) || objectType == typeof(Vector2?);
        }
    }
}
