using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class Vector2JsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderWriteable<Vector2>
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
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        void ITypedStringBuilderWriteable<Vector2>.WriteToStringBuilder(StringBuilder stringBuilder, Vector2 val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderWriteable<Vector2>.ToJsonString(Vector2 val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Vector2 value)
        {

            stringBuilder.Append("{\"x\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.x);
            stringBuilder.Append(",\"y\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.y);
            stringBuilder.Append("}");
        }

        private static string ToJsonString(Vector2 val)
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
                writer.WriteRawValue(ToJsonString((Vector2)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Vector2) || objectType == typeof(Vector2?);
        }
    }
}