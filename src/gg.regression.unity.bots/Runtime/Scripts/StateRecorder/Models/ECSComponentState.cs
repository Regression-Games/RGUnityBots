using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using Unity.Entities;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class ECSComponentState
    {
        public string name;
        public IComponentData state;

        public override string ToString()
        {
            return name;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, name);
            stringBuilder.Append(",\"state\":");
            JsonUtils.WriteECSComponentStateToStringBuilder(stringBuilder, name, state);
            stringBuilder.Append("}");
        }
    }
}