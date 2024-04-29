using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class RectJsonConverter : Newtonsoft.Json.JsonConverter
    {

        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(1000);

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, Rect? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Rect value)
        {
            stringBuilder.Append("{\"x\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.x);
            stringBuilder.Append(",\"y\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.y);
            stringBuilder.Append(",\"width\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.width);
            stringBuilder.Append(",\"height\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.height);
            stringBuilder.Append("}");
        }

        private static string ToJsonString(Rect val)
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
