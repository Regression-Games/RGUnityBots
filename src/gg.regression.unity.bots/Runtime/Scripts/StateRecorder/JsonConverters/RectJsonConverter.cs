using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class RectJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public static string ToJsonString(Rect? value)
        {
            if (value != null)
            {
                var val = value.Value;
                return "{\"x\":" + FloatJsonConverter.ToJsonString(val.x)
                                 + ",\"y\":" + FloatJsonConverter.ToJsonString(val.y)
                                 + ",\"width\":" + FloatJsonConverter.ToJsonString(val.width)
                                 + ",\"height\":" + FloatJsonConverter.ToJsonString(val.height) + "}";
            }

            return "null";
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                // raw is way faster than using the libraries
                writer.WriteRawValue(ToJsonString((Rect)value));
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
