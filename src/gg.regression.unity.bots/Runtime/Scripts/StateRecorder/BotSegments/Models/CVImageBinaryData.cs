using System;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models.CVService
{
    [JsonConverter(typeof(CVImageBinaryDataJsonConverter))]
    public class CVImageBinaryData
    {
        /**
         * <summary>This must be the image data as a JPG encoded as that is what python is expecting.  NOT just the data bytes</summary>
         */
        public byte[] data;
        public int width;
        public int height;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"width\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, width);
            stringBuilder.Append(",\n\"height\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, height);
            stringBuilder.Append(",\n\"data\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, Convert.ToBase64String(data));
            stringBuilder.Append("}");
        }
    }
}
