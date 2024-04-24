using System;
using Newtonsoft.Json;
using StateRecorder;
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
                writer.WriteRawValue("{\"controller\":" + JsonUtils.EscapeJsonString(val.runtimeAnimatorController.name)
                                                        + ",\"avatar\":" + JsonUtils.EscapeJsonString(val.avatar.name)
                                                        + ",\"applyRootMotion\":" + (val.applyRootMotion ? "true" : "false")
                                                        // enum doesn't need json escaping
                                                        + ",\"updateMode\":\"" + val.updateMode
                                                        // enum doesn't need json escaping
                                                        + "\",\"cullingMode\":\"" + val.cullingMode
                                                        + "\"}");
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
