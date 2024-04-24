using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    // NOTE: This class exists as a performance optimization as JsonConverters list model for JsonSerializerSettings scales very very poorly
    public class ColorJsonConverter : Newtonsoft.Json.JsonConverter
    {
        // re-usable and large enough to fit colors of all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(200);

        public static string ToJsonString(Color? val)
        {
            if (val == null)
            {
                return "null";
            }

            var value = val.Value;
            _stringBuilder.Clear();
            _stringBuilder.Append("{\"r\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(value.r));
            _stringBuilder.Append(",\"g\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(value.g));
            _stringBuilder.Append(",\"b\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(value.b));
            _stringBuilder.Append(",\"a\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(value.a));
            _stringBuilder.Append("}");
            return _stringBuilder.ToString();

        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // raw is way faster than using the libraries
            writer.WriteRawValue(ToJsonString((Color?)value));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Color);
        }

        public override bool CanRead => false;
    }
}
