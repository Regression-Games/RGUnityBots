using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class VectorJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public static string ToJsonStringVector2(Vector2? val)
        {
            if (val != null)
            {
                var value = val.Value;
                return "{\"x\":" + FloatJsonConverter.ToJsonString(value.x) + ",\"y\":" + FloatJsonConverter.ToJsonString(value.y) + "}";
            }

            return "null";
        }

        public static string ToJsonStringVector3(Vector3? val)
        {
            if (val != null)
            {
                var value = val.Value;
                return "{\"x\":" + FloatJsonConverter.ToJsonString(value.x) + ",\"y\":" + FloatJsonConverter.ToJsonString(value.y) + ",\"z\":" + FloatJsonConverter.ToJsonString(value.z) + "}";
            }

            return "null";
        }

        public static string ToJsonStringVector4(Vector4? val)
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
                // raw is way faster than using the libraries
                if (value is Vector2 val)
                {
                    writer.WriteRawValue(ToJsonStringVector2(val));
                }
                else if (value is Vector3 valZ)
                {
                    writer.WriteRawValue(ToJsonStringVector3(valZ));
                }
                else if (value is Vector4 valW)
                {
                    writer.WriteRawValue(ToJsonStringVector4(valW));
                }
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Vector4) || objectType == typeof(Vector3) || objectType == typeof(Vector2) || objectType == typeof(Vector4?) || objectType == typeof(Vector3?) || objectType == typeof(Vector2?);
        }
    }
}
