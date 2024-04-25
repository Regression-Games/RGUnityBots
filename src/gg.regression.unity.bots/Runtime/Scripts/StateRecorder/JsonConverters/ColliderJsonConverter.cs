using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class ColliderJsonConverter : Newtonsoft.Json.JsonConverter
    {
        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(1000);

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Collider val)
        {
            if (val == null)
            {
                stringBuilder.Append("null");
                return;
            }

            stringBuilder.Append("{\"bounds\":");
            BoundsJsonConverter.WriteToStringBuilder(stringBuilder, val.bounds);
            stringBuilder.Append(",\"isTrigger\":");
            stringBuilder.Append((val.isTrigger ? "true" : "false"));
            stringBuilder.Append("}");
        }

        private static string ToJsonString(Collider val)
        {
            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder, val);
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
                writer.WriteRawValue(ToJsonString((Collider)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Collider);
        }
    }
}
