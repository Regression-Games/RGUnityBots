using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Rigidbody2DRecordState: RigidbodyRecordState
    {
        private static readonly string TypeName = typeof(Rigidbody2D).FullName;

        public new int apiVersion = SdkApiVersion.VERSION_4;
        public new int ApiVersion()
        {
            return apiVersion;
        }

        // keep a ref to this instead of updating fields every tick
        public new Rigidbody2D rigidbody;

        public override void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, TypeName);
            stringBuilder.Append(",\"is2D\":true");
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"position\":");
            VectorJsonConverter.WriteToStringBuilderVector3(stringBuilder, rigidbody.position);
            stringBuilder.Append(",\"rotation\":");
            QuaternionJsonConverter.WriteToStringBuilder(stringBuilder, Quaternion.Euler(0, 0, rigidbody.rotation));
            stringBuilder.Append(",\"velocity\":");
            VectorJsonConverter.WriteToStringBuilderVector3(stringBuilder, rigidbody.velocity);
            stringBuilder.Append(",\"mass\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.mass);
            stringBuilder.Append(",\"drag\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.drag);
            stringBuilder.Append(",\"angularDrag\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.angularDrag);
            stringBuilder.Append(",\"gravityScale\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.gravityScale);
            stringBuilder.Append(",\"isKinematic\":");
            stringBuilder.Append((rigidbody.isKinematic ? "true" : "false"));
            stringBuilder.Append("}");
        }
    }
}
