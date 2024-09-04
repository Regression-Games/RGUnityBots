using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class RigidbodyJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderWriteable<Rigidbody>
    {

        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(4_000));

        void ITypedStringBuilderWriteable<Rigidbody>.WriteToStringBuilder(StringBuilder stringBuilder, Rigidbody val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderWriteable<Rigidbody>.ToJsonString(Rigidbody val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Rigidbody val)
        {
            if (val == null)
            {
                stringBuilder.Append("null");
                return;
            }

            stringBuilder.Append("{\"position\":");
            Vector3JsonConverter.WriteToStringBuilder(stringBuilder, val.position);
            stringBuilder.Append(",\"rotation\":");
            QuaternionJsonConverter.WriteToStringBuilder(stringBuilder, val.rotation);
            stringBuilder.Append(",\"velocity\":");
            Vector3JsonConverter.WriteToStringBuilder(stringBuilder, val.velocity);
            stringBuilder.Append(",\"mass\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, val.mass);
            stringBuilder.Append(",\"drag\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, val.drag);
            stringBuilder.Append(",\"angularDrag\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, val.angularDrag);
            stringBuilder.Append(",\"useGravity\":");
            stringBuilder.Append(val.useGravity ? "true" : "false");
            stringBuilder.Append(",\"isKinematic\":");
            stringBuilder.Append(val.isKinematic ? "true" : "false");
            stringBuilder.Append("}");
        }

        private static string ToJsonString(Rigidbody val)
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
                writer.WriteRawValue(ToJsonString((Rigidbody)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Rigidbody);
        }
    }
}
