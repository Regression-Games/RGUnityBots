using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class ButtonJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderWriteable<Button>, IBehaviourStringBuilderWritable
    {
        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(100));

        public void WriteBehaviourToStringBuilder(StringBuilder stringBuilder, Behaviour behaviour)
        {
            WriteToStringBuilder(stringBuilder, (Button)behaviour);
        }

        void ITypedStringBuilderWriteable<Button>.WriteToStringBuilder(StringBuilder stringBuilder, Button val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderWriteable<Button>.ToJsonString(Button val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Button val)
        {
            stringBuilder.Append("{\"interactable\":");
            stringBuilder.Append((val.interactable ? "true" : "false"));
            stringBuilder.Append("}");
        }

        private static string ToJsonString(Button value)
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
                writer.WriteRawValue(ToJsonString((Button)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Button);
        }
    }
}
