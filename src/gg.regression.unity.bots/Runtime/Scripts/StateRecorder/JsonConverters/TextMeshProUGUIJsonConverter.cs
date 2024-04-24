using System;
using Newtonsoft.Json;
using StateRecorder;
using TMPro;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class TextMeshProUGUIJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (TextMeshProUGUI)value;
                // raw is way faster than using the libraries
                writer.WriteRawValue("{\"text\":" + JsonUtils.EscapeJsonString(val.text)
                                                  + ",\"textStyle\":" + JsonUtils.EscapeJsonString(val.textStyle.name)
                                                  + ",\"font\":" + JsonUtils.EscapeJsonString(val.font.name)
                                                  // enum doesn't need json escaping
                                                  + ",\"fontStyle\":\"" + val.fontStyle
                                                  + "\",\"fontSize\":" + val.fontSize
                                                  + ",\"color\":" + ColorJsonConverter.ToJsonString(val.color)
                                                  + ",\"raycastTarget\":" + (val.raycastTarget ? "true" : "false")
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
            return objectType == typeof(TextMeshProUGUI);
        }
    }
}
