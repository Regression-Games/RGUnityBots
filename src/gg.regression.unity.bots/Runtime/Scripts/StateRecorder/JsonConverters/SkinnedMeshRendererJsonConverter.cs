using System;
using Newtonsoft.Json;
using UnityEngine;

namespace StateRecorder.JsonConverters
{
    public class SkinnedMeshRendererJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (SkinnedMeshRenderer)value;
                writer.WriteStartObject();
                writer.WritePropertyName("materials");
                writer.WriteStartArray();
                foreach (var valMaterial in val.materials)
                {
                    writer.WriteValue(valMaterial.name);
                }
                writer.WriteEndArray();
                // TODO: Include Lighting/Lightmapping/Probes
                writer.WritePropertyName("dynamicOcclusion");
                writer.WriteValue(val.allowOcclusionWhenDynamic);
                writer.WritePropertyName("renderingLayerMask");
                writer.WriteValue("" + val.renderingLayerMask + ": " + LayerMask.LayerToName((int)val.renderingLayerMask));
                writer.WriteEndObject();
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
