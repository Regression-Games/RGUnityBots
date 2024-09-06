using System;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine.Animations.Rigging;

namespace RegressionGames.StateRecorder.AnimationRigging.JsonConverters
{
    public class AnimationStyleJsonConverter : Newtonsoft.Json.JsonConverter
    {
        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new (1_000);

        public static void WriteToStringBuilder(StringBuilder stringBuilder, RigEffectorData.Style val)
        {

            stringBuilder.Append("{\"color\":");
            ColorJsonConverter.WriteToStringBuilder(stringBuilder, val.color);
            stringBuilder.Append(",\"position\":");
            Vector3JsonConverter.WriteToStringBuilder(stringBuilder, val.position);
            stringBuilder.Append(",\"rotation\":");
            Vector3JsonConverter.WriteToStringBuilder(stringBuilder, val.rotation);
            stringBuilder.Append(",\"size\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, val.size);
            stringBuilder.Append(",\"shape\":");
            MeshJsonConverter.WriteToStringBuilder(stringBuilder, val.shape);
            stringBuilder.Append("}");
        }

        private static string ToJsonString(RigEffectorData.Style value)
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
                writer.WriteRawValue(ToJsonString((RigEffectorData.Style)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RigEffectorData.Style);
        }
    }
}
