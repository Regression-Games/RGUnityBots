﻿using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class ULongJsonConverter: JsonConverter, ITypedStringBuilderConverter<ulong>
    {
        // re-usable and large enough to fit objects of all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(20));

        private static readonly NumberFormatInfo NumberFormatInfo = new ()
        {
            NumberDecimalDigits = 0
        };

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, ulong? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        void ITypedStringBuilderConverter<ulong>.WriteToStringBuilder(StringBuilder stringBuilder, ulong val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderConverter<ulong>.ToJsonString(ulong val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, ulong val)
        {
            if (val == 0)
            {
                stringBuilder.Append("0");
                return;
            }

            stringBuilder.Append(val.ToString(NumberFormatInfo));
        }

        private static string ToJsonString(ulong f)
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value, f);
            return _stringBuilder.Value.ToString();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            writer.WriteRawValue(ToJsonString((ulong)value));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ulong) || objectType == typeof(UInt64);
        }
    }
}
