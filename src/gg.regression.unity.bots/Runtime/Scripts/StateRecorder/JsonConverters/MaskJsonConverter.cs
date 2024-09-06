using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class MaskJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderWriteable<Mask>, IBehaviourStringBuilderWritable
    {
        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(500));

        public void WriteBehaviourToStringBuilder(StringBuilder stringBuilder, Behaviour behaviour)
        {
            WriteToStringBuilder(stringBuilder, (Mask)behaviour);
        }

        void ITypedStringBuilderWriteable<Mask>.WriteToStringBuilder(StringBuilder stringBuilder, Mask val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderWriteable<Mask>.ToJsonString(Mask val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Mask val)
        {
            stringBuilder.Append("{\"showMaskGraphic\":");
            stringBuilder.Append((val.showMaskGraphic ? "true" : "false"));
            stringBuilder.Append("}");
        }

        private static string ToJsonString(Mask value)
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value, value);
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
                writer.WriteRawValue(ToJsonString((Mask)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Mask);
        }
    }
}
