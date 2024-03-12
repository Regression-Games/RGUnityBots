using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class QuaternionJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (Quaternion)value;
                // raw is way faster than using the libraries
                writer.WriteRawValue("{\"x\":"+val.x
                                         +",\"y\":"+val.y
                                         +",\"z\":"+val.z
                                         +",\"w\":"+val.w+"}");
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Quaternion) || objectType == typeof(Quaternion?);
        }
    }
}
