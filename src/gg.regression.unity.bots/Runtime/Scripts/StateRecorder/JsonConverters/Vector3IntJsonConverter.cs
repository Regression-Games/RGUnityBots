using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class Vector3IntJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderWriteable<Vector3Int>
    {
        // re-usable and large enough to fit vectors of all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(200));

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, Vector3Int? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        void ITypedStringBuilderWriteable<Vector3Int>.WriteToStringBuilder(StringBuilder stringBuilder, Vector3Int val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderWriteable<Vector3Int>.ToJsonString(Vector3Int val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Vector3Int value)
        {
            stringBuilder.Append("{\"x\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, value.x);
            stringBuilder.Append(",\"y\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, value.y);
            stringBuilder.Append(",\"z\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, value.z);
            stringBuilder.Append("}");
        }

        public static string ToJsonString(Vector3Int val)
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value, val);
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
                writer.WriteRawValue(ToJsonString((Vector3Int)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Vector3Int) || objectType == typeof(Vector3Int?);
        }
    }
}
