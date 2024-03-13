using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
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
                // raw is way faster than using the libraries
                writer.WriteRawValue("{\"controller\":" + JsonConvert.ToString(val.runtimeAnimatorController.name)
                                                        + ",\"avatar\":" + JsonConvert.ToString(val.avatar.name)
                                                        + ",\"applyRootMotion\":" + val.applyRootMotion.ToString().ToLower()
                                                        + ",\"updateMode\":" + JsonConvert.ToString(val.updateMode)
                                                        + ",\"cullingMode\":" + JsonConvert.ToString(val.cullingMode) + "}");
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
