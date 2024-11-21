using System.Text;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.JsonConverters;

namespace StateRecorder.BotSegments.Models
{
    public class SequenceRestartCheckpoint : IStringBuilderWriteable
    {
        public int apiVersion = SdkApiVersion.VERSION_27;

        public string resourcePath;

        // NOT supported yet
        //public string segmentNumber;
        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"resourcePath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, resourcePath);
            stringBuilder.Append("}");
        }
    }
}
