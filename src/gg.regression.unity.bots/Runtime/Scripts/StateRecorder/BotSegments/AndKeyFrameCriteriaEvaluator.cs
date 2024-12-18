using System;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.BotCriteria;

namespace RegressionGames.StateRecorder.BotSegments
{
    public static class AndKeyFrameCriteriaEvaluator
    {
        public static bool Matched(bool firstSegment, int segmentNumber, bool botActionCompleted, bool validationsCompleted, KeyFrameCriteria criteria)
        {
            if (criteria.data is AndKeyFrameCriteriaData { criteriaList: not null } andCriteria)
            {
                try
                {
                    return KeyFrameEvaluator.Evaluator.MatchedHelper(firstSegment, segmentNumber, botActionCompleted, validationsCompleted, BooleanCriteria.And, andCriteria.criteriaList);
                }
                catch (Exception)
                {
                    RGDebug.LogError($"({segmentNumber}) - Bot Segment - Invalid criteria: " + andCriteria);
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
