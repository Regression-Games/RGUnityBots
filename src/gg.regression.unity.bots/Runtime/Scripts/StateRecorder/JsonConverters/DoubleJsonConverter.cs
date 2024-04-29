﻿using System;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class DoubleJsonConverter: JsonConverter
    {
        // re-usable and large enough to fit objects of all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(40);

        private static readonly NumberFormatInfo NumberFormatInfo = new ()
        {
            NumberDecimalDigits = 0
        };

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, double? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, double f)
        {
            var val = (int)f;
            var remainder = (int)((f % 1) * 10_000_000);
            // write to fixed precision of up to 7 decimal places
            // optimized to minimize toString and concat calls for all cases
            if (val == 0)
            {
                if (remainder == 0)
                {
                    // 0.0
                    stringBuilder.Append("0");
                    return;
                }

                if (remainder > 0)
                {
                    // 0.xxx
                    stringBuilder.Append("0.");
                    DecimalJsonConverter.OptimizedZeroPadTo7Digits(stringBuilder, remainder);
                    return;
                }

                // -0.xx
                stringBuilder.Append("-0.");
                DecimalJsonConverter.OptimizedZeroPadTo7Digits(stringBuilder, remainder * -1);
                return;
            }

            if (remainder == 0)
            {
                // xx.0 or -xx.0
                stringBuilder.Append(val.ToString(NumberFormatInfo));
                return;
            }

            stringBuilder.Append(val.ToString(NumberFormatInfo));
            stringBuilder.Append(".");
            // -xx.xx : xx.xx
            if (remainder < 0)
            {
                DecimalJsonConverter.OptimizedZeroPadTo7Digits(stringBuilder, remainder * -1);
            }
            else
            {
                DecimalJsonConverter.OptimizedZeroPadTo7Digits(stringBuilder, remainder);
            }

        }

        private static string ToJsonString(double f)
        {
            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder, f);
            return _stringBuilder.ToString();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            writer.WriteRawValue(ToJsonString((double)value));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(double) || objectType == typeof(Double);
        }
    }
}
