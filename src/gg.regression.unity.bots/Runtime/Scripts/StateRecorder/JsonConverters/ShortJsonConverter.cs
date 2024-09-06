using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class ShortJsonConverter: JsonConverter, ITypedStringBuilderWriteable<short>
    {
        // re-usable and large enough to fit objects of all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(20));

        private static readonly NumberFormatInfo NumberFormatInfo = new ()
        {
            NumberDecimalDigits = 0
        };

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, short? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        void ITypedStringBuilderWriteable<short>.WriteToStringBuilder(StringBuilder stringBuilder, short val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderWriteable<short>.ToJsonString(short val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, short val)
        {
            if (val == 0)
            {
                stringBuilder.Append("0");
                return;
            }

            stringBuilder.Append(val.ToString(NumberFormatInfo));
        }

        private static string ToJsonString(short f)
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
            writer.WriteRawValue(ToJsonString((short)value));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(short) || objectType == typeof(Int16);
        }
    }
}
