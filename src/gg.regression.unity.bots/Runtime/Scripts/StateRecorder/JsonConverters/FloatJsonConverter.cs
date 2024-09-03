using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class FloatJsonConverter: JsonConverter
    {
        // re-usable and large enough to fit objects of all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(20));

        private static readonly NumberFormatInfo NumberFormatInfo = new ()
        {
            NumberDecimalDigits = 0,

        };

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, float? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, float f)
        {
            var val = (long)f;
            int remainder;
            if (val == long.MinValue)
            {
                remainder = 0;
            }
            else
            {
                remainder = (int)((f % 1) * 10_000_000);
            }
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

        private static string ToJsonString(float f)
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
            writer.WriteRawValue(ToJsonString((float)value));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(float) || objectType == typeof(Single);
        }
    }
}
