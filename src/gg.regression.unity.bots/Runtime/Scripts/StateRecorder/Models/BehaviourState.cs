using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class BehaviourState : IComponentDataProvider
    {
        // update me if fields / json change
        public int apiVersion = SdkApiVersion.VERSION_4;

        public int ApiVersion()
        {
            return apiVersion;
        }

        public string name;
        public Behaviour state;

        public override string ToString()
        {
            return name;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, name);
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"state\":");
            JsonUtils.WriteBehaviourStateToStringBuilder(stringBuilder, state);
            stringBuilder.Append("}");
        }
    }
}
