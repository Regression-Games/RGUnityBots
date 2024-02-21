using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace StateRecorder.JsonConverters
{
    public class TextJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (Text)value;
                writer.WriteStartObject();
                writer.WritePropertyName("text");
                writer.WriteValue(val.text);
                writer.WritePropertyName("font");
                writer.WriteValue(val.font.name);
                writer.WritePropertyName("fontStyle");
                writer.WriteValue(val.fontStyle.ToString());
                writer.WritePropertyName("fontSize");
                writer.WriteValue(val.fontSize);
                writer.WritePropertyName("color");
                serializer.Serialize(writer, val.color, typeof(Color));
                writer.WritePropertyName("raycastTarget");
                writer.WriteValue(val.raycastTarget);

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
            return objectType == typeof(Text);
        }
    }
}