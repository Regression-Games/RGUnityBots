using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AI;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class NavMeshAgentJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderConverter<NavMeshAgent>, IBehaviourStringBuilderWritable
    {
        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(500));

        public void WriteBehaviourToStringBuilder(StringBuilder stringBuilder, Behaviour behaviour)
        {
            WriteToStringBuilder(stringBuilder, (NavMeshAgent)behaviour);
        }

        void ITypedStringBuilderConverter<NavMeshAgent>.WriteToStringBuilder(StringBuilder stringBuilder, NavMeshAgent val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderConverter<NavMeshAgent>.ToJsonString(NavMeshAgent val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, NavMeshAgent val)
        {
            stringBuilder.Append("{\"agentType\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, NavMesh.GetSettingsNameFromID(val.agentTypeID));
            stringBuilder.Append("}");
        }

        private static string ToJsonString(NavMeshAgent value)
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
