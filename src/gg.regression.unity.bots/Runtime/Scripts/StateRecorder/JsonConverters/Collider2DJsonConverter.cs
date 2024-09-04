using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class Collider2DJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderWriteable<Collider2D>
    {

        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(1000));

        void ITypedStringBuilderWriteable<Collider2D>.WriteToStringBuilder(StringBuilder stringBuilder, Collider2D val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderWriteable<Collider2D>.ToJsonString(Collider2D val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Collider2D val)
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

        private static string ToJsonString(Collider2D val)
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
                writer.WriteRawValue(ToJsonString((Collider2D)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Collider2D);
        }
    }
}
