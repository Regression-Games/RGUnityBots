using System;
using Newtonsoft.Json;
using UnityEngine;

namespace StateRecorder.JsonConverters
{
    public class RigidbodyJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (Rigidbody)value;
                writer.WriteStartObject();
                writer.WritePropertyName("position");
                writer.WriteValue(val.position);
                writer.WritePropertyName("rotation");
                writer.WriteValue(val.rotation);
                writer.WritePropertyName("velocity");
                writer.WriteValue(val.velocity);
                writer.WritePropertyName("mass");
                writer.WriteValue(val.mass);
                writer.WritePropertyName("drag");
                writer.WriteValue(val.drag);
                writer.WritePropertyName("angularDrag");
                writer.WriteValue(val.angularDrag);
                writer.WritePropertyName("useGravity");
                writer.WriteValue(val.useGravity);
                writer.WritePropertyName("isKinematic");
                writer.WriteValue(val.isKinematic);
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
            return objectType == typeof(Rigidbody);
        }
    }
}
