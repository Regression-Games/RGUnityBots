using System;
using System.Text;
using Newtonsoft.Json;
using StateRecorder;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class ButtonJsonConverter : Newtonsoft.Json.JsonConverter, IBehaviourStringBuilderWritable
    {
        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(100);

        public void WriteBehaviourToStringBuilder(StringBuilder stringBuilder, Behaviour behaviour)
        {
            WriteToStringBuilder(stringBuilder, (Button)behaviour);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Button val)
        {
            stringBuilder.Append("{\"interactable\":");
            stringBuilder.Append((val.interactable ? "true" : "false"));
            stringBuilder.Append("}");
        }

        private static string ToJsonString(Button value)
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
