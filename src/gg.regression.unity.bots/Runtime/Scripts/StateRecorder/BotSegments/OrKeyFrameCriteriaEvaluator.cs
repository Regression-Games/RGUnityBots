using System;
using RegressionGames.StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments
{
    public static class OrKeyFrameCriteriaEvaluator
    {
        public static bool Matched(KeyFrameCriteria criteria)
        {
            if (criteria.data is OrKeyFrameCriteriaData { criteriaList: not null } orCriteria)
            {
                try
                {
                    return KeyFrameEvaluator.Evaluator.MatchedHelper(BooleanCriteria.Or, orCriteria.criteriaList);
                }
                catch (Exception)
                {
                    RGDebug.LogError("Invalid bot segment criteria: " + orCriteria);
                }
            }
            else
            {
                RGDebug.LogError("Invalid bot segment criteria: " + criteria);
            }

            return false;
        }
    }
}
