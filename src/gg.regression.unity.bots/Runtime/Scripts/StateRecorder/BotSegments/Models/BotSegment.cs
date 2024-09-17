using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.Models;
using StateRecorder.BotSegments.Models;
using UnityEngine;
using UnityEngine.Serialization;

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
        public int apiVersion = SdkApiVersion.VERSION_15;

        // the highest apiVersion component included in this json.. used for compatibility checks on replay load
        public int EffectiveApiVersion => Math.Max(Math.Max(apiVersion, botAction?.EffectiveApiVersion ?? 0), endCriteria.DefaultIfEmpty().Max(a=>a?.EffectiveApiVersion ?? 0));

        /**
         * <summary>Title for this bot segment. Used for naming on the UI.</summary>
         */
        public string name;

        /**
         * <summary>Description for this bot segment. Used for naming on the UI.</summary>
         */
        public string description;

        public string sessionId;

        [FormerlySerializedAs("keyFrameCriteria")]
        public List<KeyFrameCriteria> endCriteria = new();

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

        /**
         * <summary>A HARD stop that doesn't wait for the action to finish.  Useful for stopping segments from the UI controls/etc.</summary>
         */
        public void AbortAction()
        {
            if (botAction != null)
            {
                botAction.AbortAction(Replay_SegmentNumber);
            }
        }

        /**
         * <summary>A SOFT stop that signals the action to stop as soon as possible. Most actions will stop nearly immediately, but input replay actions will finish their list first.
         * This is called once a segment's criteria have been matched and it is up to each action impl to decide how to implement this as some actions may need to finish a set of inputs before stopping.</summary>
         */
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
            var endCriteriaLength = endCriteria.Count;
            for (var i = 0; i < endCriteriaLength; i++)
            {
                endCriteria[i].ReplayReset();
            }

            if (botAction != null)
            {
                botAction.ReplayReset();
            }

            Replay_ActionStarted = false;
            Replay_Matched = false;
        }

        // Replay only - true if any of this frame's transient criteria have matched
        public bool Replay_TransientMatched => TransientMatchedHelper(endCriteria);

        private bool TransientMatchedHelper(List<KeyFrameCriteria> criteriaList)
        {
            if (criteriaList.Count == 0)
            {
                return true;
            }

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
            return false;
        }

        // used to allow transient key frame data to be somewhat evaluated in parallel / a few segments ahead
        // a segment without transient criteria will of course hold up future segments from being evaluated (even if transient)
        // current and next segment must be transient for this to really change behaviour
        public bool HasTransientCriteria => HasTransientCriteriaHelper(endCriteria);

        private bool HasTransientCriteriaHelper(List<KeyFrameCriteria> criteriaList)
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
            stringBuilder.Append("{\n\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, name );
            stringBuilder.Append(",\n\"description\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, description );
            stringBuilder.Append(",\n\"sessionId\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, sessionId);
            stringBuilder.Append(",\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"endCriteria\":[\n");
            var endCriteriaLength = endCriteria.Count;
            for (var i = 0; i < endCriteriaLength; i++)
            {
                var criteria = endCriteria[i];
                criteria.WriteToStringBuilder(stringBuilder);
                if (i + 1 < endCriteriaLength)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n],\n\"botAction\":");
            botAction.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }

        /**
         * <summary>
         * Loads all the Segments that exist in this project (for use in the Editor or in a build)
         * </summary>
         */
        public static Dictionary<string, BotSequenceEntry> LoadAllSegments()
        {
            var segments = new Dictionary<string,BotSequenceEntry>();

#if UNITY_EDITOR
            const string segmentPath = "Assets/RegressionGames/Resources/BotSegments";
            if (!Directory.Exists(segmentPath))
            {
                return segments;
            }

            segments = LoadSegmentsInDirectory(segmentPath);

#else
            // 1. check the persistentDataPath for segments
            var persistentDataPath = Application.persistentDataPath + "/RegressionGames/Resources/BotSegments";
            if (Directory.Exists(persistentDataPath))
            {
                segments = LoadSegmentsInDirectory(persistentDataPath);
            }

            // 2. load Segments from Resources, while skipping any that have already been fetched from the
            //    persistentDataPath. We will compare Segments by their filename (without extension), and by the actual
            //    Segment name
            var rgBotSequencesAsset = Resources.Load<IRGBotSequences>("RGBotSequences");
            foreach (var resourceFilename in rgBotSequencesAsset.segments)
            {
                try
                {
                    if (!segments.ContainsKey(resourceFilename))
                    {
                        var sequenceInfo = Resources.Load<TextAsset>(resourceFilename);
                        var sequenceEntry = ParseSegment(sequenceInfo.text ?? "", resourceFilename);

                        // add the new sequence if its filename doesn't already exist
                        segments.Add(resourceFilename, sequenceEntry);
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"Exception reading Sequence json file from resource path: {resourceFilename}", e);
                }
            }
#endif

            return segments;
        }

        public static BotSequenceEntry LoadSegment(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (!File.Exists(path))
            {
                return null;
            }
            
            // using var sr = new StreamReader(File.OpenRead(path));
            // var result = (path, sr.ReadToEnd());
            return BotSequence.CreateBotSequenceEntryForPath(path).Item3;
            // return ParseSegment(result.Item2, path);
        }

        /**
         * <summary>
         * Recursively look through directories for Segment and segment list files, and load them
         * </summary>
         * <param name="path">Directory to search for Segments</param>
         */
        private static Dictionary<string, BotSequenceEntry> LoadSegmentsInDirectory(string path)
        {
            var results = new Dictionary<string, BotSequenceEntry>();
            var files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories).Select(s=> s.Replace('\\','/'));
            foreach (var fileName in files)
            {
                var entry = BotSequence.CreateBotSequenceEntryForPath(fileName);

                if (entry.Item2 != null && !results.ContainsKey(entry.Item2))
                {
                    // parse the result as either a segment or segment list
                    results[entry.Item2] = entry.Item3;
                }
            }

            return results;
        }

    }
}
