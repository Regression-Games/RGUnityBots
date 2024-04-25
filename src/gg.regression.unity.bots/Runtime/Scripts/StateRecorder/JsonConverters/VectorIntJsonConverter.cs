using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class VectorIntJsonConverter : Newtonsoft.Json.JsonConverter
    {
        // re-usable and large enough to fit vectors of all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(200);

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, Vector2Int? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Vector2Int value)
        {
            stringBuilder.Append("{\"x\":");
            stringBuilder.Append(value.x.ToString());
            stringBuilder.Append(",\"y\":");
            stringBuilder.Append(value.y.ToString());
            stringBuilder.Append("}");
        }

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, Vector3Int? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Vector3Int value)
        {
            stringBuilder.Append("{\"x\":");
            stringBuilder.Append(value.x.ToString());
            stringBuilder.Append(",\"y\":");
            stringBuilder.Append(value.y.ToString());
            stringBuilder.Append(",\"z\":");
            stringBuilder.Append(value.z.ToString());
            stringBuilder.Append("}");
        }

        private static string ToJsonString(Vector2Int val)
        {
            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder, val);
            return _stringBuilder.ToString();
        }

        private static string ToJsonString(Vector3Int val)
        {
            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder, val);
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
                if (value is Vector2Int val)
                {
                    // raw is way faster than using the libraries
                    writer.WriteRawValue(ToJsonString(val));
                }
                else
                {
                    // raw is way faster than using the libraries
                    writer.WriteRawValue(ToJsonString((Vector3Int)value));
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
            return objectType == typeof(Vector3Int) || objectType == typeof(Vector2Int) || objectType == typeof(Vector3Int?) || objectType == typeof(Vector2Int?);
        }
    }
}
