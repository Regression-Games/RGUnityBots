using System.Collections.Generic;

namespace RegressionGames.GenericBots.Experimental.Rewards
{
    public class CombinationRewardModule : IRewardModule
    {

        public Dictionary<IRewardModule, float> ratios;

        public void Dispose()
        {
            foreach (var (key, value) in ratios)
            {
                key.Dispose();
            }
        }

        public float GetRewardForLastAction()
        {
            float result = 0.0f;
            foreach (var (key, value) in ratios)
            {
                result += key.GetRewardForLastAction() * value;
            }

            return result;
        }

        public void Reset()
        {
            foreach (var (key, value) in ratios)
            {
                key.Reset();
            }
        }
    }
}
