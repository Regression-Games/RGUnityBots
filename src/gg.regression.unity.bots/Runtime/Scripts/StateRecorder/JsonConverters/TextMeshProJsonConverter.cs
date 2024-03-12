using System;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class TextMeshProJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (TextMeshPro)value;

                // raw is way faster than using the libraries
                writer.WriteRawValue("{\"text\":" + JsonConvert.ToString(val.text)
                             +",\"textStyle\":" + JsonConvert.ToString(val.textStyle.name)
                             +",\"font\":" + JsonConvert.ToString(val.font.name)
                             +",\"fontStyle\":" + JsonConvert.ToString(val.fontStyle)
                             +",\"fontSize\":" + val.fontSize
                             +",\"color\":" + ColorJsonConverter.ToJsonString(val.color)
                             +",\"raycastTarget\":" + val.raycastTarget.ToString().ToLower() +"}");
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
