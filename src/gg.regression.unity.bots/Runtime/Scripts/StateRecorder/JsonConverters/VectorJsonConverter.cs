using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class VectorJsonConverter : Newtonsoft.Json.JsonConverter
    {
        // re-usable and large enough to fit vectors of all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(200));

        public static void WriteToStringBuilderVector2Nullable(StringBuilder stringBuilder, Vector2? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilderVector2(stringBuilder, f.Value);
        }

        public static void WriteToStringBuilderVector2(StringBuilder stringBuilder, Vector2 value)
        {

            stringBuilder.Append("{\"x\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.x);
            stringBuilder.Append(",\"y\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.y);
            stringBuilder.Append("}");
        }

        private static string ToJsonStringVector2(Vector2 val)
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilderVector2(_stringBuilder.Value, val);
            return _stringBuilder.Value.ToString();
        }

        public static void WriteToStringBuilderVector3Nullable(StringBuilder stringBuilder, Vector3? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilderVector3(stringBuilder, f.Value);
        }

        public static void WriteToStringBuilderVector3(StringBuilder stringBuilder, Vector3 value)
        {
            stringBuilder.Append("{\"x\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.x);
            stringBuilder.Append(",\"y\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.y);
            stringBuilder.Append(",\"z\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.z);
            stringBuilder.Append("}");
        }

        private static string ToJsonStringVector3(Vector3 val)
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilderVector3(_stringBuilder.Value, val);
            return _stringBuilder.Value.ToString();
        }

        public static void WriteToStringBuilderVector4Nullable(StringBuilder stringBuilder, Vector4? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilderVector4(stringBuilder, f.Value);
        }

        public static void WriteToStringBuilderVector4(StringBuilder stringBuilder, Vector4 value)
        {
            stringBuilder.Append("{\"x\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.x);
            stringBuilder.Append(",\"y\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.y);
            stringBuilder.Append(",\"z\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.z);
            stringBuilder.Append(",\"w\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.w);
            stringBuilder.Append("}");
        }

        private static string ToJsonStringVector4(Vector4 val)
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilderVector4(_stringBuilder.Value, val);
            return _stringBuilder.Value.ToString();
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
