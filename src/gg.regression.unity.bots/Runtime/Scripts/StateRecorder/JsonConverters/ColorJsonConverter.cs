using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    // NOTE: This class exists as a performance optimization as JsonConverters list model for JsonSerializerSettings scales very very poorly
    public class ColorJsonConverter : Newtonsoft.Json.JsonConverter
    {
        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(200));

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, Color? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Color value)
        {
            stringBuilder.Append("{\"r\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.r);
            stringBuilder.Append(",\"g\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.g);
            stringBuilder.Append(",\"b\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.b);
            stringBuilder.Append(",\"a\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, value.a);
            stringBuilder.Append("}");
        }

        private static string ToJsonString(Color val)
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
                return;
            }
            // raw is way faster than using the libraries
            writer.WriteRawValue(ToJsonString((Color)value));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Color) || objectType == typeof(Color?);
        }

        public override bool CanRead => false;
    }
}
