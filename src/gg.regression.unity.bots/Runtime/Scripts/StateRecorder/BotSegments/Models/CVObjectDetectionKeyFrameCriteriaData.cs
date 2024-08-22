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
        public int apiVersion = SdkApiVersion.VERSION_15;

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
        /// Constructor for CVObjectDetectionKeyFrameCriteriaData.
        /// </summary>
        /// <param name="textQuery">The text query used for object detection.</param>
        /// <param name="imageQuery">Currently not supported. The image query used for object detection.</param>
        /// <param name="withinRect">Optional rectangle defining the region of interest within the image.</param>
        public CVObjectDetectionKeyFrameCriteriaData(string textQuery = null, string imageQuery = null, CVWithinRect withinRect = null)
        {
            
            if (textQuery != null && imageQuery != null)
            {
                RGDebug.LogError("Both textQuery and imageQuery cannot be provided simultaneously. Use only one.");
                throw new ArgumentException("Both textQuery and imageQuery cannot be provided simultaneously. Use only one.");
            }
            else if (textQuery == null && imageQuery == null)
            {
                RGDebug.LogError("Neither textQuery nor queryImage is provided. One should be specified.");
                throw new ArgumentException("Neither textQuery nor queryImage is provided. One should be specified.");
            }

            this.textQuery = textQuery;
            this.imageQuery = imageQuery;
            this.withinRect = withinRect;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"textQuery\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, textQuery);
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"withinRect\":");
            CVWithinRectJsonConverter.WriteToStringBuilderNullable(stringBuilder, withinRect);
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
