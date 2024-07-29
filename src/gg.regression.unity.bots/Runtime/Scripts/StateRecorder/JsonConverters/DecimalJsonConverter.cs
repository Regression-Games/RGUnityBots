using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class DecimalJsonConverter: JsonConverter
    {
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(80));

        private static readonly NumberFormatInfo NumberFormatInfo = new ()
        {
            NumberDecimalDigits = 0
        };

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, decimal? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, decimal f)
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
                    OptimizedZeroPadTo7Digits(stringBuilder, remainder);
                    return;
                }

                // -0.xx
                stringBuilder.Append("-0.");
                OptimizedZeroPadTo7Digits(stringBuilder, remainder * -1);
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
                OptimizedZeroPadTo7Digits(stringBuilder, remainder * -1);
            }
            else
            {
                OptimizedZeroPadTo7Digits(stringBuilder, remainder);
            }

        }

        public static void OptimizedZeroPadTo7Digits(StringBuilder stringBuilder, int value)
        {
            if (value == 0)
            {
                stringBuilder.Append("0000000");
                return;
            }

            if (value > 999_999)
            {
                // no padding
            }
            else if (value > 99_999)
            {
                stringBuilder.Append("0");
            }
            else if (value > 9_999)
            {
                stringBuilder.Append("00");
            }
            else if (value > 999)
            {
                stringBuilder.Append("000");
            }
            else if (value > 99)
            {
                stringBuilder.Append("0000");
            }
            else if (value > 9)
            {
                stringBuilder.Append("00000");
            }
            else
            {
                stringBuilder.Append("000000");
            }

            stringBuilder.Append(value.ToString(NumberFormatInfo));
        }

        private static string ToJsonString(decimal f)
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
            writer.WriteRawValue(ToJsonString((decimal)value));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(decimal) || objectType == typeof(Decimal);
        }
    }
}
