using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;
using StateRecorder.BotSegments;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models
{

    /**
     * <summary>Used to define a sequence of BotSegment/BotSegmentList as a single bot.  This is used to load bot sequences from json or to build a new sequence using the UI</summary>
     */
    [Serializable]
    [JsonConverter(typeof(BotSequenceJsonConverter))]
    public class BotSequence
    {
        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(10_000));

        private readonly List<BotSegmentList> _segmentsToProcess = new();

        public List<BotSequenceEntry> segments = new();

        /**
         * <summary>Define the name of this sequence that will be seen in user interfaces and runtime summaries.  This SHOULD NOT be null.</summary>
         */
        public string name;

        /**
         * <summary>Description for this sequence. Used for naming on the UI.</summary>
         */
        public string description;

        /**
         * <summary>Loads a Json sequence file from a json path.  This API expects a relative path</summary>
         * <returns>(filePath(null if resource / not-writeable), resourcePath, BotSequence)</returns>
         */
        public static (string, string, BotSequence) LoadSequenceJsonFromPath(string path)
        {
            if (path.StartsWith('/') || path.StartsWith('\\'))
            {
                throw new Exception("Invalid path.  Path must be relative, not absolute in order to support editor vs production runtimes interchangeably.");
            }

            path = path.Replace('\\', '/');

            (string, string, string) sequenceJson;
            try
            {
                sequenceJson = LoadJsonResource(path);
            }
            catch (Exception)
            {
                if (path.Contains("Assets/"))
                {
                    // they were already explicit and it wasn't found
                    throw;
                }
                // try again in our resources folder
                sequenceJson = LoadJsonResource("Assets/RegressionGames/Resources/" + path);
            }

            return (sequenceJson.Item1, sequenceJson.Item2, JsonConvert.DeserializeObject<BotSequence>(sequenceJson.Item3));
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
            stringBuilder.Append(",\n\"segments\":[\n");
            var segmentsCount = segments.Count;
            for (var i = 0; i < segmentsCount; i++)
            {
                var segment = segments[i];
                segment.WriteToStringBuilder(stringBuilder);
                if (i + 1 < segmentsCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]}");
        }

        /**
         * <summary>In the Unity Editor this will create a new resource under "Assets/RegressionGames/Resources/BotSequences".  In runtime builds, it will write to "{Application.persistentDataPath}/BotSequences" .</summary>
         */
        public void SaveSequenceAsJson()
        {
            try
            {
                string directoryPath = null;
#if UNITY_EDITOR
                directoryPath = "Assets/RegressionGames/Resources/BotSequences";
#else
                directoryPath = Application.persistentDataPath + "/BotSequences";
#endif
                Directory.CreateDirectory(directoryPath);
                var filename = string.Join("-", name.Split(" "));
                var filepath = directoryPath + "/" + filename + ".json";
                foreach (var c in Path.GetInvalidPathChars())
                {
                    filepath = filepath.Replace(c, '-');
                }

                File.Delete(filepath);
                using var sw = File.CreateText(filepath);
                sw.Write(this.ToJsonString());
                sw.Close();
            }
            catch (Exception e)
            {
                throw new Exception($"Exception trying to persist BotSequence name: {name}", e);
            }
        }

        /**
         * <summary>Load the json resource at the specified path.  If .json is not on this path it will be auto appended.</summary>
         * <returns>A (FilePath (null - if loaded as resource), resourcePath, contentString) pair</returns>
         */
        public static (string, string, string) LoadJsonResource(string path)
        {
            (string, string, string)? result = null;

            var resourcePath = path;
            if (resourcePath.Contains("Resources/"))
            {
                resourcePath = resourcePath.Substring(resourcePath.LastIndexOf("Resources/") + "Resources/".Length);
            }

            var lastIndex = resourcePath.LastIndexOf(".json");
            if (lastIndex >=0)
            {
                resourcePath = resourcePath.Substring(0, lastIndex);
            }

            // document ..
#if UNITY_EDITOR
            // #if editor .. load and save files directly from the resources folder, we need the extension on the file
            var editorFilePath = path;
            if (!editorFilePath.EndsWith(".json"))
            {
                editorFilePath += ".json";
            }
            using var sr = new StreamReader(File.OpenRead(editorFilePath));
            result = (editorFilePath, resourcePath, sr.ReadToEnd());
#else
            // #if runtime .. load files from either resources, OR .. persistentDataPath.. preferring persistentDataPath as an 'override' to resources
            var runtimePath = resourcePath;

            try
            {
                // for override we need the full path with extension
                if (!runtimePath.EndsWith(".json"))
                {
                    runtimePath += ".json";
                }
                if (File.Exists(Application.persistentDataPath + "/" + runtimePath))
                {
                    using var fr = new StreamReader(File.OpenRead(Application.persistentDataPath + "/" + runtimePath));
                    result = (Application.persistentDataPath + "/" + runtimePath, resourcePath, fr.ReadToEnd());
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Exception reading json files from directory: {Application.persistentDataPath + "/" + path}", e);
            }

            try
            {
                if (!result.HasValue)
                {
                    // load from resources asset in the build
                    var json = Resources.Load<TextAsset>(resourcePath);
                    result = (null, resourcePath, json.text);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Exception reading json files from resource path: {runtimePath}", e);
            }
#endif

            if (!result.HasValue)
            {
                throw new Exception($"Error loading json resource from path: {path}.  Ensure you are loading a valid json resource path.");
            }

            return result.Value;
        }


        private void InitializeBotSequence()
        {
            _segmentsToProcess.Clear();
            foreach (var segmentEntry in segments)
            {
                switch (segmentEntry.type)
                {
                    case BotSequenceEntryType.Segment:
                        _segmentsToProcess.Add(ParseBotSegmentPath(segmentEntry.path));
                        break;
                    case BotSequenceEntryType.SegmentList:
                        _segmentsToProcess.Add(ParseBotSegmentListPath(segmentEntry.path));
                        break;
                }
            }
        }

        /**
         * <summary>Run the defined bot segments</summary>
         */
        public void Play()
        {
            InitializeBotSequence();

            var playbackController = UnityEngine.Object.FindObjectOfType<BotSegmentsPlaybackController>();
            playbackController.Stop();
            playbackController.Reset();
            playbackController.SetDataContainer(new BotSegmentsPlaybackContainer(_segmentsToProcess.SelectMany(a => a.segments)));
            playbackController.Play();
        }

        public void Stop()
        {
            var playbackController = UnityEngine.Object.FindObjectOfType<BotSegmentsPlaybackController>();
            playbackController.Stop();
            playbackController.Reset();
        }

        /**
         * <summary>
         * Adds the specified segment list to this sequence. This assumes that the path is a Unity Resource path to a bot segment list json file.
         * </summary>
         * <returns>true if segment list added, false if path doesn't exist or parsing error occurs</returns>
         */
        public bool AddBotSegmentListPath(string path)
        {
            try
            {
                ParseBotSegmentPath(path);
            }
            catch (Exception e)
            {
                RGDebug.LogWarning($"Exception parsing bot segment list at path: {path} - {e}");
                return false;
            }

            segments.Add( new BotSequenceEntry()
            {
                type = BotSequenceEntryType.SegmentList,
                path = path
            });
            return true;
        }

        /**
         * <summary>
         * Adds the specified segment list to this sequence. This assumes that the path is a Unity Resource path to a bot segment list json file.
         * </summary>
         * <exception>If the .json path specified is NOT parsable as a BotSegmentList, this API will throw an exception.</exception>
         */
        private BotSegmentList ParseBotSegmentListPath(string path)
        {
            if (path.StartsWith('/') || path.StartsWith('\\'))
            {
                throw new Exception("Invalid path.  Path must be relative, not absolute, in order to support editor vs production runtimes interchangeably.");
            }

            path = path.Replace('\\', '/');

            try
            {
                (string, string, string) jsonFile;
                try
                {
                    jsonFile = LoadJsonResource(path);
                }
                catch (Exception)
                {
                    if (path.StartsWith("Assets/"))
                    {
                        // they were already explicit and it wasn't found
                        throw;
                    }
                    // try again in our resources folder
                    jsonFile = LoadJsonResource("Assets/RegressionGames/Resources/" + path);
                }

                var segment = JsonConvert.DeserializeObject<BotSegmentList>(jsonFile.Item3);
                if (segment.EffectiveApiVersion > SdkApiVersion.CURRENT_VERSION)
                {
                    throw new Exception($"Bot segment list file contains a segment which requires SDK version {segment.EffectiveApiVersion}, but the currently installed SDK version is {SdkApiVersion.CURRENT_VERSION}");
                }

                if (string.IsNullOrEmpty(segment.name))
                {
                    segment.name = jsonFile.Item2;
                }

                return segment;
            }
            catch (Exception e)
            {
                throw new Exception($"Exception while parsing bot segment list from resource path: {path}", e);
            }
        }

        /**
         * <summary>
         * Adds the specified segment to this sequence.  This assumes that the path is a Unity Resource path to a bot segment json file.
         * NOTE: This segment will be placed in its own BotSegmentList using the description from the BotSegment.
         * </summary>
         * <returns>true if segment added, false if path doesn't exist or parsing error occurs</returns>
         */
        public bool AddBotSegmentPath(string path)
        {
            try
            {
                ParseBotSegmentPath(path);
            }
            catch (Exception e)
            {
                RGDebug.LogWarning($"Exception parsing bot segment at path: {path} - {e}");
                return false;
            }

            segments.Add( new BotSequenceEntry()
            {
                type = BotSequenceEntryType.Segment,
                path = path
            });
            return true;
        }

        /**
         * <summary>
         * Adds the specified segment to this sequence.  This assumes that the path is a Unity Resource path to a bot segment json file.
         * NOTE: This segment will be placed in its own BotSegmentList using the description from the BotSegment.
         * </summary>
         * <exception>If the .json path specified is NOT parsable as a BotSegment, this API will throw an exception.</exception>
         */
        private BotSegmentList ParseBotSegmentPath(string path)
        {
            if (path.StartsWith('/') || path.StartsWith('\\'))
            {
                throw new Exception("Invalid path.  Path must be relative, not absolute, in order to support editor vs production runtimes interchangeably.");
            }

            path = path.Replace('\\', '/');

            try
            {

                (string,string,string) jsonFile;
                try
                {
                    jsonFile = LoadJsonResource(path);
                }
                catch (Exception)
                {
                    if (path.StartsWith("Assets/"))
                    {
                        // they were already explicit and it wasn't found
                        throw;
                    }
                    // try again in our resources folder
                    jsonFile = LoadJsonResource("Assets/RegressionGames/Resources/" + path);
                }

                var segment = JsonConvert.DeserializeObject<BotSegment>(jsonFile.Item3);
                if (segment.EffectiveApiVersion > SdkApiVersion.CURRENT_VERSION)
                {
                    throw new Exception($"Bot segment file requires SDK version {segment.EffectiveApiVersion}, but the currently installed SDK version is {SdkApiVersion.CURRENT_VERSION}");
                }

                if (string.IsNullOrEmpty(segment.name))
                {
                    segment.name = jsonFile.Item2;
                }

               return new BotSegmentList(path, new List<BotSegment> {segment});
            }
            catch (Exception e)
            {
                throw new Exception($"Exception while parsing bot segment from resource path: {path}", e);
            }
        }
    }
}
