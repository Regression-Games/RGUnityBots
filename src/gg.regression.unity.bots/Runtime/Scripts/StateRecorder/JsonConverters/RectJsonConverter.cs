using System;
using Newtonsoft.Json;
using UnityEngine;

namespace StateRecorder.JsonConverters
{
    public class RectJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (Rect)value;
                writer.WriteStartObject();
                writer.WritePropertyName("x");
                writer.WriteValue(val.x);
                writer.WritePropertyName("y");
                writer.WriteValue(val.y);
                writer.WritePropertyName("w");
                writer.WriteValue(val.width);
                writer.WritePropertyName("h");
                writer.WriteValue(val.height);
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
            return objectType == typeof(Rect) || objectType == typeof(Rect?);
        }
    }
}
