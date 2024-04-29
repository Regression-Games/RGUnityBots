using System;
using System.Text;
using Newtonsoft.Json;
using StateRecorder;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class SkinnedMeshRendererJsonConverter : Newtonsoft.Json.JsonConverter
    {

        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(1_000);

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
                JsonUtils.EscapeJsonStringIntoStringBuilder(stringBuilder,val.materials[i].name);
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
            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder, val);
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
