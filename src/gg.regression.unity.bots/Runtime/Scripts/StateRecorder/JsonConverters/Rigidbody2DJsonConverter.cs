using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class Rigidbody2DJsonConverter : Newtonsoft.Json.JsonConverter
    {
        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(4_000);

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Rigidbody2D val)
        {
            if (val == null)
            {
                stringBuilder.Append("null");
                return;
            }

            stringBuilder.Append("{\"position\":");
            VectorJsonConverter.WriteToStringBuilderVector3(stringBuilder, val.position);
            stringBuilder.Append(",\"rotation\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, val.rotation);
            stringBuilder.Append(",\"velocity\":");
            VectorJsonConverter.WriteToStringBuilderVector3(stringBuilder, val.velocity);
            stringBuilder.Append(",\"mass\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, val.mass);
            stringBuilder.Append(",\"drag\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, val.drag);
            stringBuilder.Append(",\"angularDrag\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, val.angularDrag);
            stringBuilder.Append(",\"gravityScale\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, val.gravityScale);
            stringBuilder.Append(",\"isKinematic\":");
            stringBuilder.Append((val.isKinematic ? "true" : "false"));
            stringBuilder.Append("}");
        }

        private static string ToJsonString(Rigidbody2D val)
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
                writer.WriteRawValue(ToJsonString((Rigidbody2D)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Rigidbody2D);
        }
    }
}
