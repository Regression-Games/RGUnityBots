using System;
using System.Text;
using Newtonsoft.Json;
using StateRecorder;
using TMPro;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class TextMeshProJsonConverter : Newtonsoft.Json.JsonConverter
    {
        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(10_000);

        public void WriteBehaviourToStringBuilder(StringBuilder stringBuilder, Behaviour behaviour)
        {
            WriteToStringBuilder(stringBuilder, (TextMeshPro)behaviour);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, TextMeshPro val)
        {
            if (val == null)
            {
                stringBuilder.Append("null");
                return;
            }

            stringBuilder.Append("{\"text\":");
            JsonUtils.EscapeJsonStringIntoStringBuilder(stringBuilder,val.text);
            stringBuilder.Append(",\"textStyle\":");
            JsonUtils.EscapeJsonStringIntoStringBuilder(stringBuilder,val.textStyle.name);
            stringBuilder.Append(",\"font\":");
            JsonUtils.EscapeJsonStringIntoStringBuilder(stringBuilder,val.font.name);
            stringBuilder.Append(",\"fontStyle\":\"");
            stringBuilder.Append(val.fontStyle.ToString());
            stringBuilder.Append("\",\"fontSize\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, val.fontSize);
            stringBuilder.Append(",\"color\":");
            ColorJsonConverter.WriteToStringBuilder(stringBuilder, val.color);
            stringBuilder.Append(",\"raycastTarget\":");
            stringBuilder.Append((val.raycastTarget ? "true" : "false"));
            stringBuilder.Append("}");
        }

        private static string ToJsonString(TextMeshPro val)
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
