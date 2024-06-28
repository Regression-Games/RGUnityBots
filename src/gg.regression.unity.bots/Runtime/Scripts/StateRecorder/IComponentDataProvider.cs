using System.Text;

namespace RegressionGames.StateRecorder
{
    public interface IComponentDataProvider
    {
        public void WriteToStringBuilder(StringBuilder stringBuilder);
    }
}
