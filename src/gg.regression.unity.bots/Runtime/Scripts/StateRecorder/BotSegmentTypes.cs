﻿using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
// ReSharper disable UseObjectOrCollectionInitializer

namespace RegressionGames.StateRecorder
{
    [JsonConverter(typeof(BotSegmentJsonConverter))]
    [Serializable]
    public class BotSegmment
    {
        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(10_000_000);

        public string sessionId;
        public KeyFrameCriteria[] keyFrameCriteria;
        public BotAction botAction;

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
        }

        // Replay only - true if any of this frame's transient criteria have matched
        public bool Replay_TransientMatched => TransientMatchedHelper(keyFrameCriteria);

        private bool TransientMatchedHelper(KeyFrameCriteria[] criteriaList)
        {
            if (criteriaList != null)
            {
                foreach (var criteria in criteriaList)
                {
                    if (criteria.Replay_TransientMatched)
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
            stringBuilder.Append(",\n\"keyFrameCriteria\":[");
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
            stringBuilder.Append("],\n\"botAction\":");
            botAction.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }
    }

    public sealed class BotSegmentJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(BotSegmment).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            BotSegmment actionModel = new();
            ;
            actionModel.sessionId = jObject.GetValue("sessionId").ToObject<string>(serializer);
            actionModel.botAction = jObject.GetValue("botAction").ToObject<BotAction>(serializer);
            //actionModel.keyFrameCriteria = KeyFrameCriteriaArrayJsonConverter.ReadJson(reader, typeof(KeyFrameCriteria), jObject["keyFrameCriteria"], serializer);
            actionModel.keyFrameCriteria = jObject.GetValue("keyFrameCriteria").ToObject<KeyFrameCriteria[]>(serializer);
            return actionModel;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class KeyFrameCriteriaJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(KeyFrameCriteria).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            KeyFrameCriteria criteria = new();
            criteria.type = jObject["type"].ToObject<KeyFrameCriteriaType>();
            IKeyFrameCriteriaData data = null;
            switch (criteria.type)
            {
                case KeyFrameCriteriaType.NormalizedPath:
                    data = jObject["data"].ToObject<PathKeyFrameCriteriaData>(serializer);
                    break;

                default:
                    throw new JsonSerializationException($"Unsupported KeyFrameCriteria type: '{criteria.type}'");
            }

            criteria.data = data;
            return criteria;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class BotActionJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(BotAction).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            BotAction action = new();
            action.type = jObject["type"].ToObject<BotActionType>();
            IBotActionData data = null;
            switch (action.type)
            {
                case BotActionType.InputPlayback:
                    data = jObject["data"].ToObject<InputPlaybackActionData>(serializer);
                    break;

                default:
                    throw new JsonSerializationException($"Unsupported BotAction type: '{action.type}'");
            }

            action.data = data;
            return action;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    [JsonConverter(typeof(BotActionJsonConverter))]
    [Serializable]
    public class BotAction
    {
        public BotActionType type;
        public IBotActionData data;

        public void ReplayReset()
        {
            data.ReplayReset();
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"type:\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, type.ToString());
            stringBuilder.Append(",\n\"data\":");
            data.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("\n}");
        }
    }

    public interface IBotActionData
    {
        public void WriteToStringBuilder(StringBuilder stringBuilder);

        public void ReplayReset();
    }

    [Serializable]
    public class InputPlaybackActionData : IBotActionData
    {
        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.InputPlayback;
        /**
         * <summary>Used to sync up with input event times on replay to playback at proper timings.  This is the the time of the prior key frame so that we can compute the time delay to play each input once we get to this key frame.</summary>
         */
        public double startTime;
        public InputData inputData;

        public void ReplayReset()
        {
            inputData.ReplayReset();
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"startTime:\":");
            DoubleJsonConverter.WriteToStringBuilder(stringBuilder, startTime);
            stringBuilder.Append(",\n\"inputData\":");
            inputData.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("\n}");
        }
    }

    public interface IKeyFrameCriteriaData
    {
        public void WriteToStringBuilder(StringBuilder stringBuilder);
    }

    [JsonConverter(typeof(KeyFrameCriteriaJsonConverter))]
    [Serializable]
    public class KeyFrameCriteria
    {
        public KeyFrameCriteriaType type;
        public bool transient;
        public IKeyFrameCriteriaData data;

        // Replay only - used to track if transient has ever matched during replay
        [NonSerialized]
        public bool Replay_TransientMatched;

        public void ReplayReset()
        {
            Replay_TransientMatched = false;
            if (data is OrKeyFrameCriteriaData okc)
            {
                foreach (var keyFrameCriteria in okc.criteriaList)
                {
                    keyFrameCriteria.ReplayReset();
                }
            }
            else if (data is AndKeyFrameCriteriaData akc)
            {
                foreach (var keyFrameCriteria in akc.criteriaList)
                {
                    keyFrameCriteria.ReplayReset();
                }
            }
        }

        public override string ToString()
        {
            return "{type:" + type + ",transient:" + transient + ",data:" + JsonConvert.ToString(data) + "}";
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"type:\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, type.ToString());
            stringBuilder.Append(",\n\"transient\":");
            stringBuilder.Append(transient ? "true" : "false");
            stringBuilder.Append(",\n\"data\":");
            data.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("\n}");
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

        private static Dictionary<int, TransformStatus> _priorKeyFrameUIStatus = new ();
        private static Dictionary<int, TransformStatus> _priorKeyFrameGameObjectStatus = new ();

        /**
         * <summary>Publicly callable.. caches the statuses of the last passed key frame for computing delta counts from</summary>
         */
        public bool Matched(KeyFrameCriteria[] criteriaList)
        {
            bool matched = false;
            try
            {
                matched = MatchedHelper(AndOr.And, criteriaList);
                return matched;
            }
            finally
            {
                if (matched)
                {
                    var uiTransforms = InGameObjectFinder.GetInstance().GetUITransformsForCurrentFrame();
                    var gameObjectTransforms = InGameObjectFinder.GetInstance().GetGameObjectTransformsForCurrentFrame();
                    _priorKeyFrameUIStatus = uiTransforms.Item2;
                    _priorKeyFrameGameObjectStatus = gameObjectTransforms.Item2;
                }
            }
        }

        /**
         * <summary>Only to be called internally by BotSegmentTypes helper classes</summary>
         */
        internal bool MatchedHelper(AndOr andOr, KeyFrameCriteria[] criteriaList)
        {
            var uiTransforms = InGameObjectFinder.GetInstance().GetUITransformsForCurrentFrame();
            var gameObjectTransforms = InGameObjectFinder.GetInstance().GetGameObjectTransformsForCurrentFrame();

            var normalizedPathsToMatch = new List<PathKeyFrameCriteriaData>();
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
                        normalizedPathsToMatch.Add(entry.data as PathKeyFrameCriteriaData);
                        break;
                }
            }

            // process each list.. start with the ones for this tier
            if (normalizedPathsToMatch.Count > 0)
            {
                var pathResults = NormalizedPathEvaluator.Matched(normalizedPathsToMatch, _priorKeyFrameUIStatus, _priorKeyFrameGameObjectStatus, uiTransforms.Item2, gameObjectTransforms.Item2);
                var pathResultsCount = pathResults.Count;
                for (var j = 0; j < pathResultsCount; j++)
                {
                    var pathEntry = pathResults[j];
                    if (pathEntry)
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

    public static class NormalizedPathEvaluator
    {
        // Track counts from the last keyframe completion and use that as the 'prior' data
        public static List<bool> Matched(List<PathKeyFrameCriteriaData> criteriaList, Dictionary<int, TransformStatus> priorUIStatus, Dictionary<int, TransformStatus> priorGameObjectStatus, Dictionary<int, TransformStatus> uiTransforms, Dictionary<int, TransformStatus> gameObjectTransforms)
        {
            //TODO: Compute the frame deltas (Use InGameObjectFinder).. then evaluate
            var deltaUI = InGameObjectFinder.GetInstance().ComputeNormalizedPathBasedDeltaCounts(priorUIStatus, uiTransforms, out var hasUIDelta);
            var deltaGameObjects = InGameObjectFinder.GetInstance().ComputeNormalizedPathBasedDeltaCounts(priorGameObjectStatus, gameObjectTransforms, out var hasGameDelta);
            var resultList = new List<bool>();
            foreach (var criteria in criteriaList)
            {
                var normalizedPath = criteria.path;

            }

            return resultList;
        }
    }

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

    public class PathCriteriaTracker
    {
        public List<int> ids;
        public int countDespawned;
        public int countSpawned;
        public int count;
    }

    [Serializable]
    public class PathKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        public string path;
        public int removedCount;
        public int addedCount;
        public int count;
        public CountRule countRule;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"path:\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, path);
            stringBuilder.Append(",\n\"removedCount\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, removedCount);
            stringBuilder.Append(",\n\"addedCount\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, addedCount);
            stringBuilder.Append(",\n\"count\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, count);
            stringBuilder.Append(",\n\"countRule\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, countRule.ToString());
            stringBuilder.Append("\n}");
        }
    }

    [Serializable]
    public class AndKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        public KeyFrameCriteria[] criteriaList;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"criteriaList:\":[\n");
            var criteriaListLength = criteriaList.Length;
            for (var i = 0; i < criteriaListLength; i++)
            {
                var criteria = criteriaList[i];
                criteria.WriteToStringBuilder(stringBuilder);
                if (i + 1 > criteriaListLength)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("\n]}");
        }
    }


    [Serializable]
    public class OrKeyFrameCriteriaData : IKeyFrameCriteriaData
    {
        public KeyFrameCriteria[] criteriaList;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"criteriaList:\":[\n");
            var criteriaListLength = criteriaList.Length;
            for (var i = 0; i < criteriaListLength; i++)
            {
                var criteria = criteriaList[i];
                criteria.WriteToStringBuilder(stringBuilder);
                if (i + 1 > criteriaListLength)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("\n]}");
        }
    }

    public enum CountRule
    {
        Zero,
        NonZero,
        GreaterThanEqual,
        LessThanEqual
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
