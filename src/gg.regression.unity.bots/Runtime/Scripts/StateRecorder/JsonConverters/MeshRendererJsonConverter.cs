using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class MeshRendererJsonConverter : Newtonsoft.Json.JsonConverter
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
                var val = (MeshRenderer)value;
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

                strValue += "],\"dynamicOcclusion\":" + val.allowOcclusionWhenDynamic.ToString().ToLower()
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
            return objectType == typeof(MeshRenderer);
        }
    }
}
