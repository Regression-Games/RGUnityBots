using System;
using RegressionGames;
using StateRecorder.BotSegments.Models;

namespace StateRecorder.BotSegments
{
    public static class OrKeyFrameCriteriaEvaluator
    {
        public static bool Matched(KeyFrameCriteria criteria)
        {
            if (criteria.data is OrKeyFrameCriteriaData { criteriaList: not null } orCriteria)
            {
                try
                {
                    return KeyFrameEvaluator.Evaluator.MatchedHelper(AndOr.Or, orCriteria.criteriaList);
                }
                catch (Exception e)
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
