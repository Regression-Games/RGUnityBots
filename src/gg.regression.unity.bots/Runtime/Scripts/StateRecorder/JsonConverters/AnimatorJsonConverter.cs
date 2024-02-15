using System;
using Newtonsoft.Json;
using UnityEngine;

namespace StateRecorder.JsonConverters
{
    public class AnimatorJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (Animator)value;
                writer.WriteStartObject();
                writer.WritePropertyName("controller");
                writer.WriteValue(val.runtimeAnimatorController.name);
                writer.WritePropertyName("avatar");
                writer.WriteValue(val.avatar.name);
                writer.WritePropertyName("applyRootMotion");
                writer.WriteValue(val.applyRootMotion);
                writer.WritePropertyName("updateMode");
                writer.WriteValue(val.updateMode.ToString());
                writer.WritePropertyName("cullingMode");
                writer.WriteValue(val.cullingMode.ToString());
                writer.WriteEndObject();
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
