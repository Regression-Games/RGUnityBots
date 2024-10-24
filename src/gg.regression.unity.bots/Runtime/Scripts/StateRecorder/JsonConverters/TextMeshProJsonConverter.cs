using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class TextMeshProJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderConverter<TextMeshPro>
    {
        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(10_000));

        public void WriteBehaviourToStringBuilder(StringBuilder stringBuilder, Behaviour behaviour)
        {
            WriteToStringBuilder(stringBuilder, (TextMeshPro)behaviour);
        }

        void ITypedStringBuilderConverter<TextMeshPro>.WriteToStringBuilder(StringBuilder stringBuilder, TextMeshPro val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderConverter<TextMeshPro>.ToJsonString(TextMeshPro val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, TextMeshPro val)
        {
            if (val == null)
            {
                stringBuilder.Append("null");
                return;
            }

            stringBuilder.Append("{\"text\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, val.text);
            stringBuilder.Append(",\"textStyle\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, val.textStyle.name);
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

        private static string ToJsonString(TextMeshPro val)
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
                writer.WriteRawValue(ToJsonString((TextMeshPro)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TextMeshPro);
        }
    }
}
