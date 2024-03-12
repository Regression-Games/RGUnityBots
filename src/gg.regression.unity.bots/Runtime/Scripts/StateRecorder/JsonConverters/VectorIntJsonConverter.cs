using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class VectorIntJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                if (value is Vector2Int val)
                {
                    // raw is way faster than using the libraries
                    writer.WriteRawValue("{\"x\":"+val.x+",\"y\":"+val.y+"}");
                }
                else
                {
                    var valZ = (Vector3Int)value;
                    // raw is way faster than using the libraries
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
            return objectType == typeof(Vector3Int) || objectType == typeof(Vector2Int) || objectType == typeof(Vector3Int?) || objectType == typeof(Vector2Int?);
        }
    }
}
