using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Collider2DRecordState : ColliderRecordState
    {
        public new Collider2D collider;

        public override void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"is2D\":true");
            stringBuilder.Append(",\"bounds\":");
            BoundsJsonConverter.WriteToStringBuilder(stringBuilder, collider.bounds);
            stringBuilder.Append(",\"isTrigger\":");
            stringBuilder.Append((collider.isTrigger ? "true" : "false"));
            stringBuilder.Append("}");
        }

    }
}
