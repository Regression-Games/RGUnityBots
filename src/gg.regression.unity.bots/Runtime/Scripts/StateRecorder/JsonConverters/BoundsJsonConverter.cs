using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class BoundsJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderWriteable<Bounds>
    {
        // re-usable and large enough to fit bounds vectors of all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(1000));

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, Bounds? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        void ITypedStringBuilderWriteable<Bounds>.WriteToStringBuilder(StringBuilder stringBuilder, Bounds val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderWriteable<Bounds>.ToJsonString(Bounds val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Bounds value)
        {
            var center = value.center;
            var extents = value.extents;

            stringBuilder.Append("{\"center\":{\"x\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, center.x);
            stringBuilder.Append(",\"y\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, center.y);
            stringBuilder.Append(",\"z\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, center.z);
            stringBuilder.Append("},\"extents\":{\"x\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, extents.x);
            stringBuilder.Append(",\"y\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, extents.y);
            stringBuilder.Append(",\"z\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, extents.z);
            stringBuilder.Append("}}");
        }

        private static string ToJsonString(Bounds val)
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
                writer.WriteRawValue(ToJsonString((Bounds)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Bounds) || objectType == typeof(Bounds?);
        }
    }
}
