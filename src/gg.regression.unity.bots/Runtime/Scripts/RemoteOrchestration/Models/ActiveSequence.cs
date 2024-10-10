using System;
using System.Text;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine.Animations;

// ReSharper disable InconsistentNaming
namespace RegressionGames.RemoteOrchestration.Models
{
    [Serializable]
    public class ActiveSequence
    {
        public string name;
        public string resourcePath;

        [NonSerialized]
        public BotSequence sequence;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, name);
            stringBuilder.Append(",\"resourcePath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, resourcePath);
            stringBuilder.Append("}");
        }
    }
}
