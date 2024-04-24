using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class VectorIntJsonConverter : Newtonsoft.Json.JsonConverter
    {
        // re-usable and large enough to fit vectors of all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(200);

        public static string ToJsonString(Vector2Int? val)
        {
            if (val == null)
            {
                return "null";
            }

            var value = val.Value;
            _stringBuilder.Clear();
            _stringBuilder.Append("{\"x\":");
            _stringBuilder.Append(value.x.ToString());
            _stringBuilder.Append(",\"y\":");
            _stringBuilder.Append(value.y.ToString());
            _stringBuilder.Append("}");
            return _stringBuilder.ToString();

        }

        public static string ToJsonString(Vector3Int? val)
        {
            if (val == null)
            {
                return "null";
            }

            var value = val.Value;
            _stringBuilder.Clear();
            _stringBuilder.Append("{\"x\":");
            _stringBuilder.Append(value.x.ToString());
            _stringBuilder.Append(",\"y\":");
            _stringBuilder.Append(value.y.ToString());
            _stringBuilder.Append(",\"z\":");
            _stringBuilder.Append(value.z.ToString());
            _stringBuilder.Append("}");
            return _stringBuilder.ToString();
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
