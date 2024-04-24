using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class BoundsJsonConverter : Newtonsoft.Json.JsonConverter
    {
        // re-usable and large enough to fit bounds vectors of all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(200);

        public static string ToJsonString(Bounds? val)
        {
            if (val == null)
            {
                return "null";
            }

            var value = val.Value;
            var center = value.center;
            var extents = value.extents;

            _stringBuilder.Clear();
            _stringBuilder.Append("{\"center\":{\"x\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(center.x));
            _stringBuilder.Append(",\"y\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(center.y));
            _stringBuilder.Append(",\"z\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(center.z));
            _stringBuilder.Append("},\"extents\":{\"x\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(extents.x));
            _stringBuilder.Append(",\"y\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(extents.y));
            _stringBuilder.Append(",\"z\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(extents.z));
            _stringBuilder.Append("}}");
            return _stringBuilder.ToString();
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
                writer.WriteRawValue(ToJsonString((Bounds)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Bounds) || objectType == typeof(Bounds?);
        }
    }
}
