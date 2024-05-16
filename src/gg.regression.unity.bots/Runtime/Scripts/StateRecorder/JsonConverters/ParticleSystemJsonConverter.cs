using System;
using System.Text;
using Newtonsoft.Json;
using StateRecorder;
using UnityEngine;
using UnityEngine.AI;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class ParticleSystemJsonConverter : Newtonsoft.Json.JsonConverter
    {

        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(500);

        public static void WriteToStringBuilder(StringBuilder stringBuilder, ParticleSystem val)
        {
            stringBuilder.Append("{\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, val.name);
            stringBuilder.Append(",\"isPlaying\":");
            stringBuilder.Append((val.isPlaying ? "true" : "false"));
            stringBuilder.Append("}");
        }

        private static string ToJsonString(ParticleSystem value)
        {
            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder, value);
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
                // raw is way faster than using the libraries
                writer.WriteRawValue(ToJsonString((ParticleSystem)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ParticleSystem);
        }
    }
}
