using System;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

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

        public float farClipPlane;
        public float nearClipPlane;
        public float fieldOfView;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"farClipPlane\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, farClipPlane);
            stringBuilder.Append(",\"nearClipPlane\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, nearClipPlane);
            stringBuilder.Append(",\"fieldOfView\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, fieldOfView);
            stringBuilder.Append("}");
        }
    }
}
