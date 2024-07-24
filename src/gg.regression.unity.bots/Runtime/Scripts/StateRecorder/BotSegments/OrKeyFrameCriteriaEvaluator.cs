using System;
using RegressionGames.StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments
{
    public static class OrKeyFrameCriteriaEvaluator
    {
        public static bool Matched(bool firstSegment, int segmentNumber, KeyFrameCriteria criteria)
        {
            if (criteria.data is OrKeyFrameCriteriaData { criteriaList: not null } orCriteria)
            {
                try
                {
                    return KeyFrameEvaluator.Evaluator.MatchedHelper(firstSegment, segmentNumber, BooleanCriteria.Or, orCriteria.criteriaList);
                }
                catch (Exception)
                {
                    RGDebug.LogError($"({segmentNumber}) - Bot Segment - Invalid criteria: " + orCriteria);
                }
            }
            else
            {
                RGDebug.LogError($"({segmentNumber}) - Bot Segment - Invalid criteria: " + criteria);
            }

            return false;
        }
    }
}
