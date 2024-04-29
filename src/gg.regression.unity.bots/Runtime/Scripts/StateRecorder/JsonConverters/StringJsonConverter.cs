using System;
using System.Text;
using Newtonsoft.Json;
using StateRecorder;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class StringJsonConverter : Newtonsoft.Json.JsonConverter
    {

        public static void WriteToStringBuilder(StringBuilder stringBuilder, string val)
        {
            JsonUtils.EscapeJsonStringIntoStringBuilder(stringBuilder, val);
        }

        public static string ToJsonString(string val)
        {
            if (val == null)
            {
                return "null";
            }

            return JsonUtils.EscapeJsonString(val);
        }


        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteRawValue(ToJsonString((string)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }
    }
}
