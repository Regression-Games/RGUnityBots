#nullable enable
using System;
using System.Text;

namespace RegressionGames.StateRecorder
{
    public interface ITypedStringBuilderWriteable<in T> : IStringBuilderWriteable
    {
        public void WriteToStringBuilderNullable(StringBuilder stringBuilder, T? val)
        {
            if (val != null)
            {
                WriteToStringBuilder(stringBuilder, (T) val);
            }
            else
            {
                WriteToStringBuilder(stringBuilder, "null");
            }
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder, T val);

        public string ToJsonStringNullable(T? val)
        {
            if (val != null)
            {
                return ToJsonString((T) val);
            }

            return "null";
        }

        public string ToJsonString(T val);

        void IStringBuilderWriteable.WriteToStringBuilder(StringBuilder stringBuilder, object val)
        {
            if (val is T val1)
            {
                WriteToStringBuilder(stringBuilder, val1);
            }
            else
            {
                throw new Exception($"Typeof {val.GetType().FullName} does not match supported type {typeof(T).FullName}");
            }
        }

        string IStringBuilderWriteable.ToJsonString(object val)
        {
            if (val is T val1)
            {
                return ToJsonString(val1);
            }
            throw new Exception($"Typeof {val.GetType().FullName} does not match supported type {typeof(T).FullName}");
        }
    }
}
