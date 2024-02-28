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
                    writer.WriteStartObject();
                    writer.WritePropertyName("x");
                    writer.WriteValue(val.x);
                    writer.WritePropertyName("y");
                    writer.WriteValue(val.y);
                    writer.WriteEndObject();
                }
                else
                {
                    var valZ = (Vector3Int)value;
                    writer.WriteStartObject();
                    writer.WritePropertyName("x");
                    writer.WriteValue(valZ.x);
                    writer.WritePropertyName("y");
                    writer.WriteValue(valZ.y);
                    writer.WritePropertyName("z");
                    writer.WriteValue(valZ.z);
                    writer.WriteEndObject();
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