using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class TextJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderConverter<Text>, IBehaviourStringBuilderWritable
    {
        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(10_000));

        public void WriteBehaviourToStringBuilder(StringBuilder stringBuilder, Behaviour behaviour)
        {
            WriteToStringBuilder(stringBuilder, (Text)behaviour);
        }

        void ITypedStringBuilderConverter<Text>.WriteToStringBuilder(StringBuilder stringBuilder, Text val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderConverter<Text>.ToJsonString(Text val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Text val)
        {
            if (val == null)
            {
                stringBuilder.Append("null");
                return;
            }

            stringBuilder.Append("{\"text\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, val.text);
            stringBuilder.Append(",\"font\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, val.font.name);
            stringBuilder.Append(",\"fontStyle\":\"");
            stringBuilder.Append(val.fontStyle.ToString());
            stringBuilder.Append("\",\"fontSize\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, val.fontSize);
            stringBuilder.Append(",\"color\":");
            ColorJsonConverter.WriteToStringBuilder(stringBuilder, val.color);
            stringBuilder.Append(",\"raycastTarget\":");
            stringBuilder.Append(val.raycastTarget ? "true" : "false");
            stringBuilder.Append("}");
        }

        private static string ToJsonString(Text val)
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
                writer.WriteRawValue(ToJsonString((Text)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Text);
        }
    }
}
