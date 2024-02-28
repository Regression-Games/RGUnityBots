using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class BoundsJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (Bounds)value;
                var center = val.center;
                var extents = val.extents;
                writer.WriteStartObject();

                writer.WritePropertyName("center");

                writer.WriteStartObject();
                writer.WritePropertyName("x");
                writer.WriteValue(center.x);
                writer.WritePropertyName("y");
                writer.WriteValue(center.y);
                writer.WritePropertyName("z");
                writer.WriteValue(center.z);
                writer.WriteEndObject();

                writer.WritePropertyName("extents");

                writer.WriteStartObject();
                writer.WritePropertyName("x");
                writer.WriteValue(extents.x);
                writer.WritePropertyName("y");
                writer.WriteValue(extents.y);
                writer.WritePropertyName("z");
                writer.WriteValue(extents.z);
                writer.WriteEndObject();

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
            return objectType == typeof(Bounds) || objectType == typeof(Bounds?);
        }
    }
}
