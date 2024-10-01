using System;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    /**
     * Used to save Camera information to the state.
     */
    [Serializable]
    public class CameraInfo
    {

        // update me if fields / json change
        public int apiVersion = SdkApiVersion.VERSION_23;

        public int ApiVersion()
        {
            return apiVersion;
        }

        public string name;
        public float farClipPlane;
        public float nearClipPlane;
        public float fieldOfView;
        public Camera.FieldOfViewAxis fieldOfViewAxis;
        public bool orthographic;
        public float orthographicSize;
        public Vector3 position;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, name);
            stringBuilder.Append(",\"farClipPlane\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, farClipPlane);
            stringBuilder.Append(",\"nearClipPlane\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, nearClipPlane);
            stringBuilder.Append(",\"fieldOfView\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, fieldOfView);
            stringBuilder.Append(",\"fieldOfViewAxis\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, fieldOfViewAxis.ToString());
            stringBuilder.Append(",\"orthographic\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder, orthographic);
            stringBuilder.Append(",\"orthographicSize\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, orthographicSize);
            stringBuilder.Append(",\"position\":");
            Vector3JsonConverter.WriteToStringBuilder(stringBuilder, position);
            stringBuilder.Append("}");
        }
    }
}
