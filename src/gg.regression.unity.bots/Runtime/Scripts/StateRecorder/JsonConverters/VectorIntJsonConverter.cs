using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class VectorIntJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public static string ToJsonString(Vector2Int? val)
        {
            if (val != null)
            {
                var value = val.Value;
                return "{\"x\":" + value.x + ",\"y\":" + value.y + "}";
            }

            return "null";
        }

        public static string ToJsonString(Vector3Int? val)
        {
            if (val != null)
            {
                var value = val.Value;
                return "{\"x\":" + value.x + ",\"y\":" + value.y + ",\"z\":" + value.z + "}";
            }

            return "null";
        }

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
                    writer.WriteRawValue(ToJsonString(val));
                }
                else
                {
                    var valZ = (Vector3Int)value;
                    // raw is way faster than using the libraries
                    writer.WriteRawValue(ToJsonString(valZ));
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
