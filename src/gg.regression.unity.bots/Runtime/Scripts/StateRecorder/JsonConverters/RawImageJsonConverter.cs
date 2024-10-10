using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class RawImageJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderConverter<RawImage>, IBehaviourStringBuilderWritable
    {
        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(2_000));

        public void WriteBehaviourToStringBuilder(StringBuilder stringBuilder, Behaviour behaviour)
        {
            WriteToStringBuilder(stringBuilder, (RawImage)behaviour);
        }

        void ITypedStringBuilderConverter<RawImage>.WriteToStringBuilder(StringBuilder stringBuilder, RawImage val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderConverter<RawImage>.ToJsonString(RawImage val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, RawImage val)
        {
            if (val == null)
            {
                stringBuilder.Append("null");
                return;
            }

            stringBuilder.Append("{\"texture\":");
            if (val.texture == null)
            {
                stringBuilder.Append("null");
            }
            else
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, val.texture.name);
            }
            stringBuilder.Append(",\"color\":");
            ColorJsonConverter.WriteToStringBuilder(stringBuilder, val.color);
            stringBuilder.Append(",\"material\":");
            if (val.material == null)
            {
                stringBuilder.Append("null");
            }
            else
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, val.material.name);
            }
            stringBuilder.Append(",\"raycastTarget\":");
            stringBuilder.Append((val.raycastTarget ? "true" : "false"));
            stringBuilder.Append(",\"uvRect\":");
            RectJsonConverter.WriteToStringBuilder(stringBuilder, val.uvRect);
            stringBuilder.Append("}");
        }

        private static string ToJsonString(RawImage val)
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
                writer.WriteRawValue(ToJsonString((RawImage)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RawImage);
        }
    }
}
