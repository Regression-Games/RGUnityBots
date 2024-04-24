using System;
using Newtonsoft.Json;
using StateRecorder;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class ImageJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (Image)value;
                // raw is way faster than using the libraries
                writer.WriteRawValue("{\"sourceImage\":" + (val.sprite == null ? "null":JsonUtils.EscapeJsonString(val.sprite.name))
                                                         + ",\"color\":" + ColorJsonConverter.ToJsonString(val.color)
                                                         + ",\"material\":" + (val.material == null ? "null":JsonUtils.EscapeJsonString(val.material.name))
                                                         + ",\"raycastTarget\":" + (val.raycastTarget ? "true" : "false")
                                                         + ",\"preserveAspect\":" + (val.preserveAspect ? "true" : "false")
                                                         + "}");
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Image);
        }
    }
}
