using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class UShortJsonConverter: JsonConverter, ITypedStringBuilderConverter<ushort>
    {
        // re-usable and large enough to fit objects of all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(20));

        private static readonly NumberFormatInfo NumberFormatInfo = new ()
        {
            NumberDecimalDigits = 0
        };

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, ushort? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        void ITypedStringBuilderConverter<ushort>.WriteToStringBuilder(StringBuilder stringBuilder, ushort val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderConverter<ushort>.ToJsonString(ushort val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, ushort val)
        {
            if (val == 0)
            {
                stringBuilder.Append("0");
                return;
            }

            stringBuilder.Append(val.ToString(NumberFormatInfo));
        }

        private static string ToJsonString(ushort f)
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
            writer.WriteRawValue(ToJsonString((ushort)value));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ushort) || objectType == typeof(UInt16);
        }
    }
}
