using System;
using System.Text;

namespace StateRecorder.BotSegments.Models
{
    [Serializable]
    public class OrKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        public KeyFrameCriteria[] criteriaList;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"criteriaList\":[");
            var criteriaListLength = criteriaList.Length;
            for (var i = 0; i < criteriaListLength; i++)
            {
                var criteria = criteriaList[i];
                criteria.WriteToStringBuilder(stringBuilder);
                if (i + 1 > criteriaListLength)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("]}");
        }

        public override string ToString()
        {
            var sb = new StringBuilder(1000);
            WriteToStringBuilder(sb);
            return sb.ToString();
        }
    }
}
