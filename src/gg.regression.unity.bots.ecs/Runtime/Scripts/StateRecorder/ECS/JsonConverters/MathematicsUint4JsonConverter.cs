using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Unity.Mathematics;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class MathematicsUint4JsonConverter : Newtonsoft.Json.JsonConverter
    {
        // re-usable and large enough to fit vectors of all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(200));

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, uint4? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, uint4 value)
        {
            stringBuilder.Append("{\"x\":");
            UIntJsonConverter.WriteToStringBuilder(stringBuilder, value.x);
            stringBuilder.Append(",\"y\":");
            UIntJsonConverter.WriteToStringBuilder(stringBuilder, value.y);
            stringBuilder.Append(",\"z\":");
            UIntJsonConverter.WriteToStringBuilder(stringBuilder, value.z);
            stringBuilder.Append(",\"w\":");
            UIntJsonConverter.WriteToStringBuilder(stringBuilder, value.w);
            stringBuilder.Append("}");
        }

        private static string ToJsonString(uint4 val)
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
                if (value is uint4 val)
                {
                    writer.WriteRawValue(ToJsonString(val));
                }
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(uint4) || objectType == typeof(uint4?);
        }
    }
}
