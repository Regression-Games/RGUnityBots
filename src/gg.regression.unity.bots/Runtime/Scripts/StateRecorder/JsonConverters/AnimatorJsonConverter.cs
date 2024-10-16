using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class AnimatorJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderConverter<Animator>, IBehaviourStringBuilderWritable
    {
        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(1_000));

        public void WriteBehaviourToStringBuilder(StringBuilder stringBuilder, Behaviour behaviour)
        {
            WriteToStringBuilder(stringBuilder, (Animator)behaviour);
        }

        void ITypedStringBuilderConverter<Animator>.WriteToStringBuilder(StringBuilder stringBuilder, Animator val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderConverter<Animator>.ToJsonString(Animator val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Animator val)
        {
            stringBuilder.Append("{\"controller\":{\"x\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, val.runtimeAnimatorController.name);
            stringBuilder.Append(",\"avatar\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, val.avatar.name);
            stringBuilder.Append(",\"applyRootMotion\":");
            stringBuilder.Append((val.applyRootMotion ? "true" : "false"));
            stringBuilder.Append(",\"updateMode\":\"");
            stringBuilder.Append(val.updateMode.ToString());
            stringBuilder.Append("\",\"cullingMode\":\"");
            stringBuilder.Append(val.cullingMode.ToString());
            stringBuilder.Append("\"}");
        }

        private static string ToJsonString(Animator value)
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
                writer.WriteRawValue(ToJsonString((Animator)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Animator);
        }
    }
}
