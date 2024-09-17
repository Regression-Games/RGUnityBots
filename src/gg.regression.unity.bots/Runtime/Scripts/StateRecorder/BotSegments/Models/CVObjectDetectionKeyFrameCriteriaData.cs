using System;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [Serializable]
    public class CVObjectDetectionKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        // update this if this schema changes
        public int apiVersion = SdkApiVersion.VERSION_19;

        /// <summary>
        /// The text query used for object detection.
        /// This string is used to describe or identify the object to be detected in the image.
        /// </summary>
        [CanBeNull]
        public string textQuery;

        /// <summary>
        /// The image query used for object detection.
        /// This string represents a base64-encoded image to be used as a reference for object detection.
        /// </summary>
        [CanBeNull]
        public string imageQuery;
   
        [CanBeNull]
        public CVWithinRect withinRect;

        /// <summary>
        /// The threshold to accept a returned match from the object detection model. Returned matches with a confidence score less than this threshold are ignored.
        /// </summary>
        public float? threshold;

        /// <summary>
        /// Constructor for CVObjectDetectionKeyFrameCriteriaData.
        /// </summary>
        /// <param name="textQuery">The text query used for object detection.</param>
        /// <param name="imageQuery">Currently not supported. The image query used for object detection.</param>
        /// <param name="withinRect">Optional rectangle defining the region of interest within the image.</param>
        /// <param name="threshold">Optional threshold to accept a returned match from the object detection model. Returned matches with a confidence score less than this threshold are ignored.</param>
        public CVObjectDetectionKeyFrameCriteriaData(string textQuery = null, string imageQuery = null, CVWithinRect withinRect = null, float? threshold = null)
        {
            
            if (textQuery != null && imageQuery != null)
            {
                RGDebug.LogError("Both textQuery and imageQuery cannot be provided simultaneously. Use only one.");
                throw new ArgumentException("Both textQuery and imageQuery cannot be provided simultaneously. Use only one.");
            }
            else if (textQuery == null && imageQuery == null)
            {
                RGDebug.LogError("Neither textQuery nor imageQuery is provided. One should be specified.");
                throw new ArgumentException("Neither textQuery nor imageQuery is provided. One should be specified.");
            }

            this.textQuery = textQuery;
            this.imageQuery = imageQuery;
            this.withinRect = withinRect;
            this.threshold = threshold;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"textQuery\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, textQuery);
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"withinRect\":");
            CVWithinRectJsonConverter.WriteToStringBuilderNullable(stringBuilder, withinRect);
            stringBuilder.Append(",\"threshold\":");
            FloatJsonConverter.WriteToStringBuilderNullable(stringBuilder, threshold);
            stringBuilder.Append("}");
        }

        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(1000));

        public override string ToString()
        {
            WriteToStringBuilder(_stringBuilder.Value);
            return _stringBuilder.Value.ToString();
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
