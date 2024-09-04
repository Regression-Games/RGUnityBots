using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RigidbodyRecordState: IComponentDataProvider
    {

        private static readonly string TypeName = typeof(Rigidbody).FullName;

        public int apiVersion = SdkApiVersion.VERSION_4;
        public int ApiVersion()
        {
            return apiVersion;
        }

        // keep a ref to this instead of updating fields every tick
        public Rigidbody rigidbody;

        public virtual void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, TypeName);
            stringBuilder.Append(",\"is2D\":false");
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"position\":");
            Vector3JsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.position);
            stringBuilder.Append(",\"rotation\":");
            QuaternionJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.rotation);
            stringBuilder.Append(",\"velocity\":");
            Vector3JsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.velocity);
            stringBuilder.Append(",\"mass\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.mass);
            stringBuilder.Append(",\"drag\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.drag);
            stringBuilder.Append(",\"angularDrag\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.angularDrag);
            stringBuilder.Append(",\"useGravity\":");
            stringBuilder.Append((rigidbody.useGravity ? "true" : "false"));
            stringBuilder.Append(",\"isKinematic\":");
            stringBuilder.Append((rigidbody.isKinematic ? "true" : "false"));
            stringBuilder.Append("}");
        }
    }
}
