using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class SkinnedMeshRendererJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderConverter<SkinnedMeshRenderer>
    {

        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(1_000));

        void ITypedStringBuilderConverter<SkinnedMeshRenderer>.WriteToStringBuilder(StringBuilder stringBuilder, SkinnedMeshRenderer val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderConverter<SkinnedMeshRenderer>.ToJsonString(SkinnedMeshRenderer val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, SkinnedMeshRenderer val)
        {
            if (val == null)
            {
                stringBuilder.Append("null");
                return;
            }

            stringBuilder.Append("{\"materials\":[");

            var valMaterialLength = val.materials.Length;
            for (var i = 0; i < valMaterialLength; i++)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, val.materials[i].name);
                if (i + 1 < valMaterialLength)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("],\"dynamicOcclusion\":");
            stringBuilder.Append(val.allowOcclusionWhenDynamic ? "true" : "false");

            // TODO: Include Lighting/Lightmapping/Probes
            stringBuilder.Append(",\"renderingLayerMask\":\"");
            stringBuilder.Append(val.renderingLayerMask).Append(": ").Append(LayerMask.LayerToName((int)val.renderingLayerMask));
            stringBuilder.Append("\"}");
        }

        private static string ToJsonString(SkinnedMeshRenderer val)
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value, val);
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
                writer.WriteRawValue(ToJsonString((SkinnedMeshRenderer)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(SkinnedMeshRenderer);
        }
    }
}
