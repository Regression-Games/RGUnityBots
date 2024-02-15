using System;
using Newtonsoft.Json;
using UnityEngine.UI;

namespace StateRecorder.JsonConverters
{
    public class MaskJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (Mask)value;
                writer.WriteStartObject();
                writer.WritePropertyName("showMaskGraphic");
                writer.WriteValue(val.showMaskGraphic);
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
            return objectType == typeof(Mask);
        }
    }
}
