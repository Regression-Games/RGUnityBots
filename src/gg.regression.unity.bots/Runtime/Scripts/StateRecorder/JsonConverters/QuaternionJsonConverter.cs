using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class QuaternionJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public static string ToJsonString(Quaternion? val)
        {
            if (val != null)
            {
                var value = val.Value;
                return "{\"x\":" + FloatJsonConverter.ToJsonString(value.x) + ",\"y\":" + FloatJsonConverter.ToJsonString(value.y) + ",\"z\":" + FloatJsonConverter.ToJsonString(value.z) + ",\"w\":" + FloatJsonConverter.ToJsonString(value.w) + "}";
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
                var val = (Quaternion)value;
                // raw is way faster than using the libraries
                writer.WriteRawValue(ToJsonString(val));
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
