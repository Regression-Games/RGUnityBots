using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder.BotSegments
{
    public sealed class KeyFrameEvaluator
    {
        public static readonly KeyFrameEvaluator Evaluator = new ();

        private static Dictionary<int, TransformStatus> _priorKeyFrameUIStatus = new ();
        private static Dictionary<int, TransformStatus> _priorKeyFrameGameObjectStatus = new ();

        public void Reset()
        {
            _priorKeyFrameUIStatus.Clear();
            _priorKeyFrameGameObjectStatus.Clear();
        }

        public string GetUnmatchedCriteria()
        {
            if (_unmatchedCriteria.Count == 0)
            {
                return null;
            }

            var sb = new StringBuilder(1000);
            var unmatchedCriteriaCount = _unmatchedCriteria.Count;
            for (var i = 0; i < unmatchedCriteriaCount; i++)
            {
                sb.Append(_unmatchedCriteria[i]);
                if (i + 1 < unmatchedCriteriaCount)
                {
                    sb.Append("\r\n");
                }
            }

            return sb.ToString();
        }

        private List<string> _unmatchedCriteria = new(1000);
        private List<string> _newUnmatchedCriteria = new(1000);

        /**
         * <summary>Publicly callable.. caches the statuses of the last passed key frame for computing delta counts from</summary>
         */
        public bool Matched(KeyFrameCriteria[] criteriaList)
        {
            _newUnmatchedCriteria.Clear();
            bool matched = false;
            matched = MatchedHelper(AndOr.And, criteriaList);
            if (matched)
            {
                _unmatchedCriteria.Clear();
                _newUnmatchedCriteria.Clear();
                var uiTransforms = InGameObjectFinder.GetInstance().GetUITransformsForCurrentFrame();
                var gameObjectTransforms = InGameObjectFinder.GetInstance().GetGameObjectTransformsForCurrentFrame();
                // copy the dictionaries
                _priorKeyFrameUIStatus = uiTransforms.Item2.ToDictionary(a => a.Key, a => a.Value);
                _priorKeyFrameGameObjectStatus = gameObjectTransforms.Item2.ToDictionary(a=>a.Key, a=>a.Value);
            }
            else
            {
                (_unmatchedCriteria, _newUnmatchedCriteria) = (_newUnmatchedCriteria, _unmatchedCriteria);
                _newUnmatchedCriteria.Clear();
            }
            return matched;
        }

        /**
         * <summary>Only to be called internally by BotSegmentTypes helper classes</summary>
         */
        internal bool MatchedHelper(AndOr andOr, KeyFrameCriteria[] criteriaList)
        {
            var uiTransforms = InGameObjectFinder.GetInstance().GetUITransformsForCurrentFrame();
            var gameObjectTransforms = InGameObjectFinder.GetInstance().GetGameObjectTransformsForCurrentFrame();

            var normalizedPathsToMatch = new List<KeyFrameCriteria>();
            var orsToMatch = new List<KeyFrameCriteria>();
            var andsToMatch = new List<KeyFrameCriteria>();
            //var pathsToMatch = new List<KeyFrameCriteria>();
            //var xPathsToMatch = new List<KeyFrameCriteria>();

            var length = criteriaList.Length;
            for (var i = 0; i < length; i++)
            {
                var entry = criteriaList[i];
                if (entry.transient && entry.Replay_TransientMatched)
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
                var pathResults = NormalizedPathCriteriaEvaluator.Matched(normalizedPathsToMatch, _priorKeyFrameUIStatus, _priorKeyFrameGameObjectStatus, uiTransforms.Item2, gameObjectTransforms.Item2);
                var pathResultsCount = pathResults.Count;
                for (var j = 0; j < pathResultsCount; j++)
                {
                    var pathEntry = pathResults[j];
                    if (pathEntry == null)
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
                            _newUnmatchedCriteria.Add(pathEntry);
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
                    var m = OrKeyFrameCriteriaEvaluator.Matched(orEntry);
                    if (m)
                    {
                        if (andOr == AndOr.Or)
                        {
                            return true;
                        }

                        orEntry.Replay_TransientMatched = true;
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
                    var m = AndKeyFrameCriteriaEvaluator.Matched(andEntry);
                    if (m)
                    {
                        if (andOr == AndOr.Or)
                        {
                            return true;
                        }

                        andEntry.Replay_TransientMatched = true;
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
}
