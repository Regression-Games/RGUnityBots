using System;
using Newtonsoft.Json;
using UnityEngine.AI;

namespace StateRecorder.JsonConverters
{
    public class NavMeshAgentJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (NavMeshAgent)value;
                writer.WriteStartObject();
                writer.WritePropertyName("agentType");
                writer.WriteValue(NavMesh.GetSettingsNameFromID(val.agentTypeID));
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
            return objectType == typeof(NavMeshAgent);
        }
    }
}
