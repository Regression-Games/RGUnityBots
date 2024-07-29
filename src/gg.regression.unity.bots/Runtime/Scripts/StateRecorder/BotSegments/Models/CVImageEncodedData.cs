using System;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models.CVSerice
{
    [JsonConverter(typeof(CVImageEncodedDataJsonConverter))]
    public class CVImageEncodedData
    {
        /**
         * <summary>This must be the image data as a base64 JPG encoded as that is what python is expecting.  NOT just the data bytes</summary>
         */
        public string data;
        public int width;
        public int height;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"width\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, width);
            stringBuilder.Append(",\n\"height\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, height);
            stringBuilder.Append(",\n\"data\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, data);
            stringBuilder.Append("}");
        }
    }
}
