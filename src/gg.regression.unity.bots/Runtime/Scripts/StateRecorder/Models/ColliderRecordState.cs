using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class ColliderRecordState: IComponentDataProvider
    {
        public Collider collider;

        public virtual void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"is2D\":false");
            stringBuilder.Append(",\"bounds\":");
            BoundsJsonConverter.WriteToStringBuilder(stringBuilder, collider.bounds);
            stringBuilder.Append(",\"isTrigger\":");
            stringBuilder.Append((collider.isTrigger ? "true" : "false"));
            stringBuilder.Append("}");
        }
    }
}
