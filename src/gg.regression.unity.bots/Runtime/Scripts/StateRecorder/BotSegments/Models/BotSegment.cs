using System;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [JsonConverter(typeof(BotSegmentJsonConverter))]
    [Serializable]
    public class BotSegment
    {
        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(10_000);

        public string sessionId;
        public KeyFrameCriteria[] keyFrameCriteria;
        public BotAction botAction;

        // Replay only - if this was fully matched (still not done until actions also completed)
        [NonSerialized]
        public bool Replay_Matched;

        // Replay only - numbers the segments in the replay data
        [NonSerialized]
        public int Replay_Number;

        // Replay only - tracks if we have started the action for this bot segment
        [NonSerialized]
        public bool Replay_ActionStarted;

        // Replay only - tracks if we have completed the action for this bot segment
        public bool Replay_ActionCompleted => botAction.IsCompleted;

        // Replay only
        public void ReplayReset()
        {
            if (keyFrameCriteria != null)
            {
                var keyFrameCriteriaLength = keyFrameCriteria.Length;
                for (var i = 0; i < keyFrameCriteriaLength; i++)
                {
                    keyFrameCriteria[i].ReplayReset();
                }
            }

            if (botAction != null)
            {
                botAction.ReplayReset();
            }

            Replay_ActionStarted = false;
            Replay_Matched = false;
        }

        // Replay only - true if any of this frame's transient criteria have matched
        public bool Replay_TransientMatched => TransientMatchedHelper(keyFrameCriteria);

        private bool TransientMatchedHelper(KeyFrameCriteria[] criteriaList)
        {
            if (criteriaList != null)
            {
                foreach (var criteria in criteriaList)
                {
                    if (criteria.transient && criteria.Replay_TransientMatched)
                    {
                        return true;
                    }

                    if (criteria.data is OrKeyFrameCriteriaData okc)
                    {
                        var has = TransientMatchedHelper(okc.criteriaList);
                        if (has)
                        {
                            return true;
                        }
                    }
                    else if (criteria.data is AndKeyFrameCriteriaData akc)
                    {
                        var has = TransientMatchedHelper(akc.criteriaList);
                        if (has)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // used to allow transient key frame data to be somewhat evaluated in parallel / a few segments ahead
        // a segment without transient criteria will of course hold up future segments from being evaluated (even if transient)
        // current and next segment must be transient for this to really change behaviour
        public bool HasTransientCriteria => HasTransientCriteriaHelper(keyFrameCriteria);

        private bool HasTransientCriteriaHelper(KeyFrameCriteria[] criteriaList)
        {
            if (criteriaList != null)
            {
                foreach (var criteria in criteriaList)
                {
                    if (criteria.transient)
                    {
                        return true;
                    }

                    if (criteria.data is OrKeyFrameCriteriaData okc)
                    {
                        var has = HasTransientCriteriaHelper(okc.criteriaList);
                        if (has)
                        {
                            return true;
                        }
                    }
                    else if (criteria.data is AndKeyFrameCriteriaData akc)
                    {
                        var has = HasTransientCriteriaHelper(akc.criteriaList);
                        if (has)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public string ToJsonString()
        {
            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder);
            return _stringBuilder.ToString();
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"sessionId\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, sessionId);
            stringBuilder.Append(",\n\"keyFrameCriteria\":[\n");
            var keyFrameCriteriaLength = keyFrameCriteria.Length;
            for (var i = 0; i < keyFrameCriteriaLength; i++)
            {
                var criteria = keyFrameCriteria[i];
                criteria.WriteToStringBuilder(stringBuilder);
                if (i + 1 < keyFrameCriteriaLength)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("\n],\n\"botAction\":");
            botAction.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }
    }
}
