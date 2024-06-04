using System;
using RegressionGames;
using StateRecorder.BotSegments.Models;

namespace StateRecorder.BotSegments
{
    public static class AndKeyFrameCriteriaEvaluator
    {
        public static bool Matched(KeyFrameCriteria criteria)
        {
            if (criteria.data is AndKeyFrameCriteriaData { criteriaList: not null } andCriteria)
            {
                try
                {
                    return KeyFrameEvaluator.Evaluator.MatchedHelper(AndOr.And, andCriteria.criteriaList);
                }
                catch (Exception e)
                {
                    RGDebug.LogError("Invalid bot segment criteria: " + andCriteria);
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
