using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [Serializable]
    public class OrKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        public int apiVersion = SdkApiVersion.VERSION_1;

        public List<KeyFrameCriteria> criteriaList;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"criteriaList\":[");
            var criteriaListLength = criteriaList.Count;
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

        public int EffectiveApiVersion()
        {
            return Math.Max(apiVersion, criteriaList.DefaultIfEmpty().Max(a => a?.EffectiveApiVersion ?? 0));
        }
    }
}
