using RegressionGames.StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.KeyMoments
{
    /**
     * Implemented by classes that provide key moment evaluations.
     */
    public interface IKeyMomentEvaluator
    {
        BotSegment IsKeyMoment(long tickNumber, long keyMomentNumber);
    }
}
