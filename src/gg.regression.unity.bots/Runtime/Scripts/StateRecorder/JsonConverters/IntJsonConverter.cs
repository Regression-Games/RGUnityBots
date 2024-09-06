using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class IntJsonConverter: JsonConverter, ITypedStringBuilderWriteable<int>
    {
        // re-usable and large enough to fit objects of all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(20));

        private static readonly NumberFormatInfo NumberFormatInfo = new ()
        {
            NumberDecimalDigits = 0
        };

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, int? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        void ITypedStringBuilderWriteable<int>.WriteToStringBuilder(StringBuilder stringBuilder, int val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderWriteable<int>.ToJsonString(int val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, int val)
        {
            if (val == 0)
            {
                stringBuilder.Append("0");
                return;
            }

            stringBuilder.Append(val.ToString(NumberFormatInfo));
        }

        private static string ToJsonString(int f)
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
            writer.WriteRawValue(ToJsonString((int)value));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(int) || objectType == typeof(Int32);
        }
    }
}
