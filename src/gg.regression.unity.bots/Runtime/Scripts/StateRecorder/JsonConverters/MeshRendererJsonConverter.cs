using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class MeshRendererJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderConverter<MeshRenderer>
    {

        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(500));

        void ITypedStringBuilderConverter<MeshRenderer>.WriteToStringBuilder(StringBuilder stringBuilder, MeshRenderer val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderConverter<MeshRenderer>.ToJsonString(MeshRenderer val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, MeshRenderer val)
        {
            //TODO: Implement more support for meshes
            stringBuilder.Append("{\"materials\":[");
            bool first = true;
            foreach (var valMaterial in val.materials)
            {
                if (!first)
                {
                    stringBuilder.Append(",");
                }
                StringJsonConverter.WriteToStringBuilder(stringBuilder, valMaterial.name);
                first = false;
            }

            stringBuilder.Append("],\"dynamicOcclusion\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder, val.allowOcclusionWhenDynamic);
            stringBuilder.Append(",\"renderingLayerMask\":\"");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, val.renderingLayerMask + ": " + LayerMask.LayerToName((int)val.renderingLayerMask));
            stringBuilder.Append("}");
        }

        private static string ToJsonString(MeshRenderer value)
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
                writer.WriteRawValue(ToJsonString((MeshRenderer)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(MeshRenderer);
        }
    }
}
