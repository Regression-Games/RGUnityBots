﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [JsonConverter(typeof(BotSegmentJsonConverter))]
    [Serializable]
    public class BotSegment
    {

        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(10_000));

        // versioning support for bot segments in the SDK, the is for this top level schema only
        // update this if this top level schema changes
        public int apiVersion = SdkApiVersion.VERSION_1;

        // the highest apiVersion component included in this json.. used for compatibility checks on replay load
        public int EffectiveApiVersion => Math.Max(Math.Max(apiVersion, botAction?.EffectiveApiVersion ?? 0), keyFrameCriteria.DefaultIfEmpty().Max(a=>a?.EffectiveApiVersion ?? 0));

        public string sessionId;
        public List<KeyFrameCriteria> keyFrameCriteria = new();
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

        public void OnGUI(Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            if (botAction != null)
            {
                botAction.OnGUI(currentTransforms, currentEntities);
            }
        }

        // Replay only - called at least once per frame
        public bool ProcessAction(Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            if (botAction == null)
            {
                Replay_ActionStarted = true;
            }
            else
            {
                if (!Replay_ActionStarted)
                {
                    botAction.StartAction(Replay_SegmentNumber, currentTransforms, currentEntities);
                    Replay_ActionStarted = true;
                }
                return botAction.ProcessAction(Replay_SegmentNumber, currentTransforms, currentEntities, out error);
            }

            error = null;
            return false;
        }

        public void StopAction(Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            if (botAction != null)
            {
                botAction.StopAction(Replay_SegmentNumber, currentTransforms, currentEntities);
            }
        }

        // Replay only
        public void ReplayReset()
        {
            if (keyFrameCriteria != null)
            {
                var keyFrameCriteriaLength = keyFrameCriteria.Count;
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

        private bool TransientMatchedHelper(List<KeyFrameCriteria> criteriaList)
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

        private bool HasTransientCriteriaHelper(List<KeyFrameCriteria> criteriaList)
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
            var keyFrameCriteriaLength = keyFrameCriteria.Count;
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
