using System;
using System.Text;
using JetBrains.Annotations;

namespace RegressionGames.StateRecorder
{
    public interface IStringBuilderWriteable
    {

        public void WriteToStringBuilderNullable(StringBuilder stringBuilder, [CanBeNull] object val)
        {
            if (val != null)
            {
                WriteToStringBuilder(stringBuilder, val);
            }
            else
            {
                WriteToStringBuilder(stringBuilder, "null");
            }
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder, object val);

        public string ToJsonStringNullable([CanBeNull] object val)
        {
            if (val != null)
            {
                return ToJsonString(val);
            }

            return "null";
        }

        public string ToJsonString(object val);
    }

}
