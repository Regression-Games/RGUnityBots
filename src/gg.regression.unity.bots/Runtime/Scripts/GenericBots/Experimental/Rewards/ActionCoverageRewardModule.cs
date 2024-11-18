using System.Collections.Generic;
using System.Linq;
using RegressionGames.ActionManager;
using UnityEngine;

namespace RegressionGames.GenericBots.Experimental.Rewards
{
    /// <summary>
    /// Action coverage reward module.
    /// This implements a novelty reward to encourage variety of action usages.
    /// </summary>
    public class ActionCoverageRewardModule : IRewardModule
    {

        // keep the prior action summary and reward when new or less frequently used actions are selected
        private RGActionUsageSummary _priorActionUsage;


        // Reward the agent for the novelty of the action(s) used
        // This is 1/N, where N is the number of times the used action has been used previously.
        // This reward has a range of 0 to 1.
        public float GetRewardForLastAction()
        {
            var newActionUsage = RGActionRuntimeCoverageAnalysis.GetActionUsageSummary();
            if (newActionUsage == null)
            {
                return 0.0f; // no reward - effectively don't consider this reward type
            }

            try
            {
                if (_priorActionUsage == null)
                {
                    return 1.0f;
                }
                // else

                // compare used actions for deltas or new action usages
                foreach (var (rgGameAction, newMetrics) in newActionUsage.usedActionMetrics)
                {
                    if (!_priorActionUsage.usedActionMetrics.TryGetValue(rgGameAction, out var priorMetrics))
                    {
                        return 1.0f; // new action used for first time, good reward
                    }

                    // we could go through this whole dictionary and find the 'lowest' number of invocations for a new action in case there were multiple...
                    // but we're going to optimize performance here to just pick the first.. the learning algorithm should naturally work out over many iterations to still use the less used actions
                    var delta = newMetrics.invocations - priorMetrics.invocations;
                    if (delta > 0)
                    {
                        return 1.0f / newMetrics.invocations;   // 1/N reward based on number of usages of this action
                    }
                }

                return 0.0f; // no new usages
            }
            finally
            {
                _priorActionUsage = newActionUsage;
            }
        }

        public void Reset()
        {
            _priorActionUsage = null;
        }

        public void Dispose()
        {
        }
    }
}
