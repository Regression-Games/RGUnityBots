using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class RawImageJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (RawImage)value;
                // raw is way faster than using the libraries
                writer.WriteRawValue("{\"texture\":" + JsonConvert.ToString(val.texture?.name)
                +",\"color\":" + ColorJsonConverter.ToJsonString(val.color)
                +",\"material\":" + JsonConvert.ToString(val.material?.name)
                +",\"raycastTarget\":" + val.raycastTarget.ToString().ToLower()
                +",\"uvRect\":" +  RectJsonConverter.ToJsonString(val.uvRect)+"}");
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RawImage);
        }
    }
}
