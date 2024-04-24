using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class VectorJsonConverter : Newtonsoft.Json.JsonConverter
    {
        // re-usable and large enough to fit vectors of all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(200);

        public static string ToJsonStringVector2(Vector2? val)
        {
            if (val == null)
            {
                return "null";
            }

            var value = val.Value;
            _stringBuilder.Clear();
            _stringBuilder.Append("{\"x\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(value.x));
            _stringBuilder.Append(",\"y\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(value.y));
            _stringBuilder.Append("}");
            return _stringBuilder.ToString();
        }

        public static string ToJsonStringVector3(Vector3? val)
        {
            if (val == null)
            {
                return "null";
            }

            var value = val.Value;
            _stringBuilder.Clear();
            _stringBuilder.Append("{\"x\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(value.x));
            _stringBuilder.Append(",\"y\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(value.y));
            _stringBuilder.Append(",\"z\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(value.z));
            _stringBuilder.Append("}");
            return _stringBuilder.ToString();
        }

        public static string ToJsonStringVector4(Vector4? val)
        {
            if (val == null)
            {
                return "null";
            }

            var value = val.Value;
            _stringBuilder.Clear();
            _stringBuilder.Append("{\"x\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(value.x));
            _stringBuilder.Append(",\"y\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(value.y));
            _stringBuilder.Append(",\"z\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(value.z));
            _stringBuilder.Append(",\"w\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(value.w));
            _stringBuilder.Append("}");
            return _stringBuilder.ToString();
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
