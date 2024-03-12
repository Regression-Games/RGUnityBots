using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class VectorJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                // raw is way faster than using the libraries
                if (value is Vector2 val)
                {
                    writer.WriteRawValue("{\"x\":"+val.x+",\"y\":"+val.y+"}");
                }
                else
                {
                    var valZ = (Vector3)value;
                    writer.WriteRawValue("{\"x\":"+valZ.x+",\"y\":"+valZ.y+",\"z\":"+valZ.z+"}");
                }
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Vector3) || objectType == typeof(Vector2) || objectType == typeof(Vector3?) || objectType == typeof(Vector2?);
        }
    }
}
