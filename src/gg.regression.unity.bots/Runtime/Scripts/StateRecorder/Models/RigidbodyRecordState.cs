using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RigidbodyRecordState
    {

        public string path;
        public string normalizedPath;

        // keep a ref to this instead of updating fields every tick
        public Rigidbody rigidbody;

        public virtual void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"path\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, path);
            stringBuilder.Append(",\"normalizedPath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, normalizedPath);
            stringBuilder.Append(",\"is2D\":false");
            stringBuilder.Append(",\"position\":");
            VectorJsonConverter.WriteToStringBuilderVector3(stringBuilder, rigidbody.position);
            stringBuilder.Append(",\"rotation\":");
            QuaternionJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.rotation);
            stringBuilder.Append(",\"velocity\":");
            VectorJsonConverter.WriteToStringBuilderVector3(stringBuilder, rigidbody.velocity);
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
