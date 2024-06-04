using System;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateRecorder;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class NavMeshAgentJsonConverter : Newtonsoft.Json.JsonConverter, IBehaviourStringBuilderWritable
    {
        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(500);

        public void WriteBehaviourToStringBuilder(StringBuilder stringBuilder, Behaviour behaviour)
        {
            WriteToStringBuilder(stringBuilder, (NavMeshAgent)behaviour);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, NavMeshAgent val)
        {
            stringBuilder.Append("{\"agentType\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, NavMesh.GetSettingsNameFromID(val.agentTypeID));
            stringBuilder.Append("}");
        }

        private static string ToJsonString(NavMeshAgent value)
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
                writer.WriteRawValue(ToJsonString((NavMeshAgent)value));
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
