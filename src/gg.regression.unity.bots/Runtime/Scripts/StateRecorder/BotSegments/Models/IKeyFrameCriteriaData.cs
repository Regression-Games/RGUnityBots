using System.Text;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    public interface IKeyFrameCriteriaData : IStringBuilderWriteable
    {
        public int EffectiveApiVersion();
    }
}
