using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class ShortJsonConverter: JsonConverter
    {
        // re-usable and large enough to fit objects of all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(20);

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
