using System;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    public class CVImageRequestData
    {
        /**
         * <summary>This must be the image data as a PNG/JPG/etc encoded as that is what python is expecting.  NOT just the data bytes</summary>
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
