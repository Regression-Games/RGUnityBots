using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using StateRecorder;
using UnityEngine;

namespace RegressionGames.StateRecorder
{
    public class BotSegmment
    {
        public KeyFrameCriteria[] keyFrameCriteria;
        public IBotAction botAction;
    }

    public interface IBotAction
    {

    }

    [Serializable]
    public class KeyFrameCriteria
    {
        public KeyFrameCriteriaType type;
        public bool transient;
        public Dictionary<string, object> data;

        // used to track if transient has ever matched
        [NonSerialized]
        public bool TransientMatched;

        public override string ToString()
        {
            return "{type:" + type + ",transient:" + transient + ",data:" + JsonConvert.ToString(data) + "}";
        }
    }

    public enum AndOr
    {
        And,
        Or
    }

    public sealed class KeyFrameEvaluator
    {
        public static readonly KeyFrameEvaluator Evaluator = new ();

        public bool Matched(AndOr andOr, List<KeyFrameCriteria> criteriaList, IDictionary<int, RecordedGameObjectState> priorState, IDictionary<int, RecordedGameObjectState> currentState)
        {
            var normalizedPathsToMatch = new List<KeyFrameCriteria>();
            var orsToMatch = new List<KeyFrameCriteria>();
            var andsToMatch = new List<KeyFrameCriteria>();
            //var pathsToMatch = new List<KeyFrameCriteria>();
            //var xPathsToMatch = new List<KeyFrameCriteria>();

            var length = criteriaList.Count;
            for (var i = 0; i < length; i++)
            {
                var entry = criteriaList[i];
                if (entry.transient && entry.TransientMatched)
                {
                    if (andOr == AndOr.Or)
                    {
                        return true;
                    }
                    continue;
                }

                switch (entry.type)
                {
                    case KeyFrameCriteriaType.And:
                        andsToMatch.Add(entry);
                        break;
                    case KeyFrameCriteriaType.Or:
                        orsToMatch.Add(entry);
                        break;
                    case KeyFrameCriteriaType.NormalizedPath:
                        normalizedPathsToMatch.Add(entry);
                        break;
                }
            }

            // process each list.. start with the ones for this tier
            if (normalizedPathsToMatch.Count > 0)
            {
                var pathResults = NormalizedPathEvaluator.Matched(normalizedPathsToMatch, priorState, currentState);
                var pathResultsCount = pathResults.Count;
                for (var j = 0; j < pathResultsCount; j++)
                {
                    var pathEntry = pathResults[j];
                    var m = pathEntry.Item2;
                    if (m)
                    {
                        if (andOr == AndOr.Or)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (andOr == AndOr.And)
                        {
                            return false;
                        }
                    }
                }
            }

            var orCount = orsToMatch.Count;
            if (orCount > 0)
            {
                for (var j = 0; j < orCount; j++)
                {
                    var orEntry = orsToMatch[j];
                    var m = OrKeyFrameCriteriaEvaluator.Matched(orEntry, priorState, currentState);
                    if (m)
                    {
                        if (andOr == AndOr.Or)
                        {
                            return true;
                        }
                        orEntry.TransientMatched = true;
                    }
                    else
                    {
                        if (andOr == AndOr.And)
                        {
                            return false;
                        }
                    }
                }
            }

            var andCount = andsToMatch.Count;
            if (andCount > 0)
            {
                for (var j = 0; j < andCount; j++)
                {
                    var andEntry = andsToMatch[j];
                    var m = AndKeyFrameCriteriaEvaluator.Matched(andEntry, priorState, currentState);
                    if (m)
                    {
                        if (andOr == AndOr.Or)
                        {
                            return true;
                        }
                        andEntry.TransientMatched = true;
                    }
                    else
                    {
                        if (andOr == AndOr.And)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }

    // somewhat efficiently goes through the state one time evaluating all the path matches as we go.. doesn't assume AND or OR , just gives the results to be evaluated by the caller
    public static class NormalizedPathEvaluator
    {
        public static List<(KeyFrameCriteria, bool)> Matched(List<KeyFrameCriteria> normalizedPathsToMatch, IDictionary<int, RecordedGameObjectState> priorState, IDictionary<int, RecordedGameObjectState> currentState)
        {
            // build up the state delta dictionary first - uses path hash for key
            var dictionary = new Dictionary<int, PathCriteriaTracker>();

            var normalizedPathsToMatchCount = normalizedPathsToMatch.Count;
            for (var i = 0; i < normalizedPathsToMatchCount; i++)
            {
                var entry = normalizedPathsToMatch[i];
                if (entry.data.TryGetValue("normalizedPath", out var cl) && cl != null)
                {

                }
            }

            // sum up the number of spawns / de-spawns / totals for each required path

            /**
             * go through the new state and add up the totals
             * - track the ids for each path
             *
             * go through the old state
             *  - track paths that have had despawns
             *  - track paths that have had spawns
             */

            var currentStateCount = currentState.Count;
            for (var i = 0; i < currentStateCount; i++)
            {
                var stateEntry = currentState[i];
            }


        }
    }

    public static class OrKeyFrameCriteriaEvaluator
    {
        public static bool Matched(KeyFrameCriteria criteria, IDictionary<int, RecordedGameObjectState> priorState, IDictionary<int, RecordedGameObjectState> currentState)
        {
            if (criteria.data.TryGetValue("criteriaList", out var cl) && cl != null)
            {
                try
                {
                    var criteriaList = JsonConvert.DeserializeObject<List<KeyFrameCriteria>>(cl.ToString());
                    return KeyFrameEvaluator.Evaluator.Matched(AndOr.Or, criteriaList, priorState, currentState);
                }
                catch (Exception e)
                {
                    RGDebug.LogError("Invalid bot segment criteria: " + cl);
                }
            }
            else
            {
                RGDebug.LogError("Invalid bot segment criteria: " + criteria);
            }

            return false;
        }
    }

    public static class AndKeyFrameCriteriaEvaluator
    {
        public static bool Matched(KeyFrameCriteria criteria, IDictionary<int, RecordedGameObjectState> priorState, IDictionary<int, RecordedGameObjectState> currentState)
        {
            if (criteria.data.TryGetValue("criteriaList", out var cl) && cl != null)
            {
                try
                {
                    var criteriaList = JsonConvert.DeserializeObject<List<KeyFrameCriteria>>(cl.ToString());
                    return KeyFrameEvaluator.Evaluator.Matched(AndOr.And, criteriaList, priorState, currentState);
                }
                catch (Exception e)
                {
                    RGDebug.LogError("Invalid bot segment criteria: " + cl);
                }
            }
            else
            {
                RGDebug.LogError("Invalid bot segment criteria: " + criteria);
            }

            return false;
        }
    }

    public class PathCriteriaTracker
    {
        public List<int> ids;
        public int countDespawned;
        public int countSpawned;
        public int count;
    }

    [Serializable]
    public class PathKeyFrameCriteria
    {
        public string path;
        public int countDespawned;
        public int countSpawned;
        public int count;
        public CountRule countRule;
    }

    public enum CountRule
    {
        Equal,
        GreaterThan,
        LessThan
    }

    public enum KeyFrameCriteriaType
    {
        Or,
        And,
        UIPixelHash,
        NormalizedPath,

        //FUTURE
        //Path,
        //XPath,
        //BehaviourComplete
    }

    public enum BotActionType
    {
        None,
        InputPlayback,

        //FUTURE
        //Timer,
        //FrameCount,
        //Behaviour,
        //AgentBuilder,
        //OrchestratedInput,
        //RandomMouse_ClickPixel,
        //RandomMouse_ClickObject,
        //RandomKeyboard_Key,
        //RandomKeyboard_ActionableKey,
        //RandomJoystick_Axis1|Axis2|Axis3,Key

    }
}
