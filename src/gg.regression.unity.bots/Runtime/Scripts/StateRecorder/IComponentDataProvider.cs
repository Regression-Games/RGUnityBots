using System.Text;

namespace RegressionGames.StateRecorder
{
    public interface IComponentDataProvider
    {
        public int ApiVersion();
        public void WriteToStringBuilder(StringBuilder stringBuilder);
    }
}
