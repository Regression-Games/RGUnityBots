using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class MaskJsonConverter : Newtonsoft.Json.JsonConverter, IBehaviourStringBuilderWritable
    {
        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(500);

        public void WriteBehaviourToStringBuilder(StringBuilder stringBuilder, Behaviour behaviour)
        {
            WriteToStringBuilder(stringBuilder, (Mask)behaviour);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Mask val)
        {
            stringBuilder.Append("{\"showMaskGraphic\":");
            stringBuilder.Append((val.showMaskGraphic ? "true" : "false"));
            stringBuilder.Append("}");
        }

        private static string ToJsonString(Mask value)
        {
            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder, value);
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
