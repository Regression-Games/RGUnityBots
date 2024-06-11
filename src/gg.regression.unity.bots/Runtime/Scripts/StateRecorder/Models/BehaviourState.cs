using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class BehaviourState
    {
        public string name;
        public string path;
        public string normalizedPath;
        public Behaviour state;

        public override string ToString()
        {
            return name;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, name);
            stringBuilder.Append(",\"path\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, path);
            stringBuilder.Append(",\"normalizedPath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, normalizedPath);
            stringBuilder.Append(",\"state\":");
            JsonUtils.WriteBehaviourStateToStringBuilder(stringBuilder, state);
            stringBuilder.Append("}");
        }
    }
}
