using System;
using Newtonsoft.Json;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class ButtonJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (Button)value;
                // raw is way faster than using the libraries
                writer.WriteRawValue("{\"interactable\":" + (val.interactable ? "true" : "false") + "}");
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
