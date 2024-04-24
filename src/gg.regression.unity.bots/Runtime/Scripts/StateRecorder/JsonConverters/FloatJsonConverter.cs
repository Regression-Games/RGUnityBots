using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class FloatJsonConverter: JsonConverter
    {
        public static string ToJsonString(float? f)
        {
            if (f == null)
            {
                return "null";
            }
            var val = (int)f;
            var remainder = (int)((f % 1) * 10_000_000);
            // write to fixed precision of up to 7 decimal places
            // optimized to minimize toString and concat calls for all cases
            if (val == 0)
            {
                if (remainder == 0)
                {
                    // 0.0
                    return "0";
                }

                if (remainder > 0)
                {
                    // 0.xxx
                    return "0." + remainder.ToString("D7");
                }

                // -0.xx
                return "-0." + (remainder * -1).ToString("D7");
            }

            if (remainder == 0)
            {
                // xx.0 or -xx.0
                return val.ToString();
            }

            if (remainder < 0)
            {
                // -xx.xx
                return val + "." + (remainder * -1).ToString("D7");
            }

            return val + "." + remainder.ToString("D7");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var f = (float)value;
            writer.WriteRawValue(ToJsonString(f));
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
