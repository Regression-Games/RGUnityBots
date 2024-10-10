using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class MeshFilterJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderConverter<MeshFilter>
    {
        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(500));

        void ITypedStringBuilderConverter<MeshFilter>.WriteToStringBuilder(StringBuilder stringBuilder, MeshFilter val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderConverter<MeshFilter>.ToJsonString(MeshFilter val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, MeshFilter val)
        {
            stringBuilder.Append("{\"mesh\":");
            if (val.mesh == null)
            {
                stringBuilder.Append("null");
            }
            else
            {
                MeshJsonConverter.WriteToStringBuilder(stringBuilder, val.mesh);
            }
            stringBuilder.Append("}");
        }

        private static string ToJsonString(MeshFilter value)
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value, value);
            return _stringBuilder.Value.ToString();
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
                writer.WriteRawValue(ToJsonString((MeshFilter)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(MeshFilter);
        }
    }
}
