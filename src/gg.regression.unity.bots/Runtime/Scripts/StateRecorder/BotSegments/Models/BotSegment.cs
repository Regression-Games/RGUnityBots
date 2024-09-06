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
        
        public static List<BotSequenceEntry> LoadAllSegments()
        {
            var segments = new List<BotSequenceEntry>();
            
#if UNITY_EDITOR
            const string sequencePath = "Assets/RegressionGames/Resources/BotSegments";
            if (!Directory.Exists(sequencePath))
            {
                return new List<BotSequenceEntry>();
            }

            segments = LoadSegmentsInDirectory(sequencePath);
#else
            // 1. check the persistentDataPath for segments
            var persistentDataPath = Application.persistentDataPath + "/BotSegments";
            if (Directory.Exists(persistentDataPath))
            {
                segments = LoadSegmentsInDirectoryForRuntime(persistentDataPath);
            }
        
            // 2. load Segments from Resources, and ensure that no Segments have duplicate names
            const string runtimePath = "BotSegments";
            segments = segments.Concat(LoadSegmentsInDirectoryForRuntime(runtimePath)).ToList();
#endif

            return segments;
        }
        
        private static List<BotSequenceEntry> LoadSegmentsInDirectory(string path)
        {
            var results = new List<BotSequenceEntry>();
            var directories = Directory.EnumerateDirectories(path);
            foreach (var directory in directories)
            {
                var files = Directory.GetFiles(directory, "*.json");
                foreach (var fileName in files)
                {
                    using var sr = new StreamReader(File.OpenRead(fileName));
                    var result = (fileName, sr.ReadToEnd());
                    var segment = ParseSegment(result.Item2, fileName);
                    if (segment != null)
                    {
                        results.Add(segment);
                    }   
                }

                results = results.Concat(
                    LoadSegmentsInDirectory(directory)
                ).ToList();
            }

            return results;
        }
        
        private static List<BotSequenceEntry> LoadSegmentsInDirectoryForRuntime(string path)
        {
            var results = new List<BotSequenceEntry>();
            var directories = Directory.EnumerateDirectories(path);
            foreach (var directory in directories)
            {
                var jsons = Resources.LoadAll(directory, typeof(TextAsset));
                foreach (var jsonObject in jsons)
                {
                    try
                    {
                        var json = (jsonObject as TextAsset)?.text ?? "";
                        var segment = ParseSegment(json, jsonObject.name);

                        // don't add segments with duplicate names
                        if (results.Any(s => s.entryName == segment.entryName))
                        {
                            continue;
                        }
                        
                        results.Add(segment);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Exception reading a Segment json file from resource path: {directory}", e);
                    }
                }

                results = results.Concat(
                    LoadSegmentsInDirectoryForRuntime(directory)
                ).ToList();
            }

            return results;
        }
        
        private static BotSequenceEntry ParseSegment(string json, string fileName)
        {
            try
            {
                var entry = new BotSequenceEntry
                {
                    path = fileName,
                };

                try
                {
                    var segment = JsonConvert.DeserializeObject<BotSegmentList>(json);
                    entry.description = segment.description;
                    entry.entryName = segment.name;
                    entry.type = BotSequenceEntryType.SegmentList;
                }
                catch
                {
                    try
                    {
                        var segmentList = JsonConvert.DeserializeObject<BotSegment>(json);
                        entry.description = segmentList.description;
                        entry.entryName = segmentList.name;
                        entry.type = BotSequenceEntryType.Segment;
                    }
                    catch
                    {
                        Debug.LogError($"RGSequenceEditor Could not parse Bot Segment file: {fileName}");
                        throw;
                    }
                }

                return entry;
            }
            catch (Exception exception)
            {
                Debug.Log($"RGSequenceEditor could not open Bot Sequence Entry: {exception}");
            }

            return null;
        }
    }
}
