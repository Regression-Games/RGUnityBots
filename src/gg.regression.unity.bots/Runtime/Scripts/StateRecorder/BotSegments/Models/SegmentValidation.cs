using System;
using System.Text;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.JsonConverters;

namespace StateRecorder.BotSegments.Models
{
    [Serializable]
    public class SegmentValidation
    {
        
        // api version for this top level schema, update if we add/remove/change fields here
        public int apiVersion = SdkApiVersion.VERSION_28;

        public SegmentValidationType type;
        public ISegmentValidationData data;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, type.ToString());
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"data\":");
            data.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }

    }
}