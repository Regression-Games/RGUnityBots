using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    // NOTE: This class exists as a performance optimization as JsonConverters list model for JsonSerializerSettings scales very very poorly
    public class ColorJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public static string ToJsonString(Color? val)
        {
            if (val != null)
            {
                var value = val.Value;
                return "{\"r\":" + FloatJsonConverter.ToJsonString(value.r)
                                 + ",\"g\":" + FloatJsonConverter.ToJsonString(value.g)
                                 + ",\"b\":" + FloatJsonConverter.ToJsonString(value.b)
                                 + ",\"a\":" + FloatJsonConverter.ToJsonString(value.a)
                                 + "}";
            }

            return "null";
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // raw is way faster than using the libraries
            writer.WriteRawValue(ToJsonString((Color?)value));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Color);
        }

        public override bool CanRead => false;
    }
}
