using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [JsonConverter(typeof(BotSegmentJsonConverter))]
    [Serializable]
    public class BotSegment
    {
        // these values reference key moments in the development of the SDK for bot segments
        public const int SDK_API_VERSION_1 = 1; // initial version with and/or/normalizedPath criteria and mouse/keyboard input actions
        public const int SDK_API_VERSION_2 = 2; // added mouse pixel and object random clicking actions

        // Update this when new features are used in the SDK
        public const int CURRENT_SDK_API_VERSION = SDK_API_VERSION_2;

        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(10_000));

        // versioning support for bot segments in the SDK, the is for this top level schema only
        // update this if this top level schema changes
        public int apiVersion = SDK_API_VERSION_1;

        // the highest apiVersion component included in this json.. used for compatibility checks on replay load
        public int EffectiveApiVersion => Math.Max(Math.Max(apiVersion, botAction?.EffectiveApiVersion ?? 0), keyFrameCriteria.DefaultIfEmpty().Max(a=>a?.EffectiveApiVersion ?? 0));

        public string sessionId;
        public KeyFrameCriteria[] keyFrameCriteria;
        public BotAction botAction;

        // Replay only - if this was fully matched (still not done until actions also completed)
        [NonSerialized]
        public bool Replay_Matched;

        // Replay only - numbers the segments in the replay data
        [NonSerialized]
        public int Replay_SegmentNumber;

        // Replay only - tracks if we have started the action for this bot segment
        [NonSerialized]
        public bool Replay_ActionStarted;

        // Replay only - tracks if we have completed the action for this bot segment
        // returns true if botAction.IsCompleted || botAction.IsCompleted==null && Replay_Matched
        public bool Replay_ActionCompleted => botAction == null || (botAction.IsCompleted ?? Replay_Matched);

        // Replay only - called at least once per frame
        public void ProcessAction()
        {
            botAction?.ProcessAction();
        }

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
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value);
            return _stringBuilder.Value.ToString();
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"sessionId\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, sessionId);
            stringBuilder.Append(",\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"keyFrameCriteria\":[\n");
            var keyFrameCriteriaLength = keyFrameCriteria.Length;
            for (var i = 0; i < keyFrameCriteriaLength; i++)
            {
                var criteria = keyFrameCriteria[i];
                criteria.WriteToStringBuilder(stringBuilder);
                if (i + 1 < keyFrameCriteriaLength)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n],\n\"botAction\":");
            botAction.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }
    }
}
