using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace StateRecorder.JsonConverters
{
    public class RawImageJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (RawImage)value;
                writer.WriteStartObject();
                writer.WritePropertyName("texture");
                writer.WriteValue(val.texture?.name);
                writer.WritePropertyName("color");
                serializer.Serialize(writer, val.color, typeof(Color));
                writer.WritePropertyName("material");
                writer.WriteValue(val.material?.name);
                writer.WritePropertyName("raycastTarget");
                writer.WriteValue(val.raycastTarget);
                writer.WritePropertyName("uvRect");
                writer.WriteValue(val.uvRect);
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
            return objectType == typeof(RawImage);
        }
    }
}
