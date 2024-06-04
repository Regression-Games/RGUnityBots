using System.Text;

namespace StateRecorder.BotSegments.Models
{
    public interface IKeyFrameCriteriaData
    {
        public void WriteToStringBuilder(StringBuilder stringBuilder);
    }
}
