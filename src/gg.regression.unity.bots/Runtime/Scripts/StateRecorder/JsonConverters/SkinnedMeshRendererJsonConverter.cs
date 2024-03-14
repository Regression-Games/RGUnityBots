using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
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
                // TODO: Include Lighting/Lightmapping/Probes
                var val = (SkinnedMeshRenderer)value;
                var strValue = "{\"materials\":[";

                bool first = true;
                foreach (var valMaterial in val.materials)
                {
                    if (!first)
                    {
                        strValue += ",";
                    }

                    strValue += "\"" + valMaterial.name + "\"";
                    first = false;
                }

                strValue += "],\"dynamicOcclusion\":" + (val.allowOcclusionWhenDynamic ? "true" : "false")
                                                      + ",\"renderingLayerMask\":\"" + val.renderingLayerMask + ": " + LayerMask.LayerToName((int)val.renderingLayerMask)
                                                      + "\"}";
                writer.WriteRawValue(strValue);
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
