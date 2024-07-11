using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class UnityObjectFallbackJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteRawValue("{}");
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public static bool Convertable(Type objectType)
        {
            if (typeof(Behaviour).IsAssignableFrom(objectType))
            {
                // in this case, any Behaviour should just be empty unless it has a custom converter
                // this is to avoid major performance impacts to state capture
                return true;
            }

            // (*huge performance impact*) let the default object serializer do its thing
            return false;
        }

        public override bool CanConvert(Type objectType)
        {
            return Convertable(objectType);
        }
    }
}
