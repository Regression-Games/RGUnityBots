using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.BotSegments.Models
{

    /**
     * <summary>Used to define a sequence of BotSegment/BotSegmentList as a single bot.  This is used to load bot sequences from json or to build a new sequence using the UI</summary>
     */
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
         */
        public static BotSequence LoadSequenceJsonFromPath(string path)
        {
            if (path.StartsWith('/') || path.StartsWith('\\'))
            {
                throw new Exception("Invalid path.  Path must be relative, not absolute in order to support editor vs production runtimes interchangeably.");
            }

            path = path.Replace('\\', '/');

            (string,string) sequenceJson;
            try
            {
                sequenceJson = LoadJsonResource(path);
            }
            catch (Exception)
            {
                if (path.StartsWith("Assets/"))
                {
                    // they were already explicit and it wasn't found
                    throw;
                }
                // try again in our resources folder
                sequenceJson = LoadJsonResource("Assets/RegressionGames/Resources/" + path);
            }

            return JsonConvert.DeserializeObject<BotSequence>(sequenceJson.Item2);
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
            stringBuilder.Append("{\n\"description\":");
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
                var filepath = directoryPath + "/" + name + ".json";
                foreach (var c in Path.GetInvalidFileNameChars())
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
         * <returns>A (filename, contentString) pair</returns>
         */
        private static (string, string) LoadJsonResource(string path)
        {
            (string, string)? result = null;

            if (!path.EndsWith(".json"))
            {
                path += ".json";
            }
            // document ..
#if UNITY_EDITOR
            // #if editor .. load and save files directly from the resources folder
            using var sr = new StreamReader(File.OpenRead(path));
            result = (path,sr.ReadToEnd());
#else
            // #if runtime .. load files from either resources, OR .. persistentDataPath.. preferring persistentDataPath as an 'override' to resources
            var runtimePath = path;
            // get only the part AFTER Resources in the path
            if (runtimePath.Contains("Resources/"))
            {
                runtimePath = runtimePath.Substring(runtimePath.LastIndexOf("Resources/") + "Resources/".Length);
            }

            try
            {
                if (File.Exists(Application.persistentDataPath + "/" + runtimePath))
                {
                    using var fr = new StreamReader(File.OpenRead(Application.persistentDataPath + "/" + runtimePath));
                    result = (runtimePath, fr.ReadToEnd());
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
                    var json = Resources.Load<TextAsset>(runtimePath);
                    result = (runtimePath, json.text);
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

        /**
         * <returns>A List of (filename, contentString) pairs.</returns>
         */
        private static List<(string, string)> LoadJsonResourcesFromDirectory(string path)
        {
            List<(string, string)> results = new();
            // document ..
#if UNITY_EDITOR
            // #if editor .. load and save files directly from the resources folder
            var filesInDirectory = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly);

            // sort by numeric value of entries (not string comparison of filenames)
            IOrderedEnumerable<string> entries;
            try
            {
                // try to order the files as numbered file names (this is how our bot segment replays are written)
                entries = filesInDirectory.OrderBy(e => int.Parse(e.Substring(0, e.IndexOf('.'))));
            }
            catch (Exception)
            {
                // if filenames aren't all numbers, order lexicographically
                entries = filesInDirectory.OrderBy(e => e.Substring(0, e.IndexOf('.')));
            }

            foreach (var entry in entries)
            {
                using var sr = new StreamReader(File.OpenRead(entry));
                results.Add((entry,sr.ReadToEnd()));
            }
#else
            // #if runtime .. load files from either resources, OR .. persistentDataPath.. preferring persistentDataPath as an 'override' to resources

            var runtimePath = path;
            // get only the part AFTER Resources in the path
            if (runtimePath.Contains("Resources/"))
            {
                runtimePath = runtimePath.Substring(runtimePath.LastIndexOf("Resources/") + "Resources/".Length);
            }

            try
            {
                var filesInDirectory = Directory.GetFiles(Application.persistentDataPath + "/" + runtimePath, "*.json", SearchOption.TopDirectoryOnly);

                // sort by numeric value of entries (not string comparison of filenames)
                IOrderedEnumerable<string> entries;
                try
                {
                    // try to order the files as numbered file names (this is how our bot segment replays are written)
                    entries = filesInDirectory.OrderBy(e => int.Parse(e.Substring(0, e.IndexOf('.'))));
                }
                catch (Exception)
                {
                    // if filenames aren't all numbers, order lexicographically
                    entries = filesInDirectory.OrderBy(e => e.Substring(0, e.IndexOf('.')));
                }

                foreach (var entry in entries)
                {
                    using var sr = new StreamReader(File.OpenRead(entry));
                    results.Add((entry,sr.ReadToEnd()));
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Exception reading json files from directory: {Application.persistentDataPath + "/" + runtimePath}", e);
            }

            try
            {
                if (results.Count == 0)
                {
                    // load from resources asset in the build
                    //TODO: Find a way to sort these based on their path name; thanks a lot Unity
                    var jsons = Resources.LoadAll(runtimePath, typeof(TextAsset));
                    foreach (var jsonObject in jsons)
                    {
                        var json = (jsonObject as TextAsset).text;
                        results.Add(("TODO: Find Resource Name", json));
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Exception reading json files from resource path: {runtimePath}", e);
            }
#endif

            if (results.Count == 0)
            {
                throw new Exception($"Error loading json resources from path: {path}.  Must include at least 1 json.  Ensure you are loading a valid json directory path.");
            }

            return results;
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
                (string,string) jsonFile;
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

                var segment = JsonConvert.DeserializeObject<BotSegmentList>(jsonFile.Item2);
                if (segment.EffectiveApiVersion > SdkApiVersion.CURRENT_VERSION)
                {
                    throw new Exception($"Bot segment list file contains a segment which requires SDK version {segment.EffectiveApiVersion}, but the currently installed SDK version is {SdkApiVersion.CURRENT_VERSION}");
                }

                if (string.IsNullOrEmpty(segment.name))
                {
                    segment.name = jsonFile.Item1;
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

                (string,string) jsonFile;
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

                var segment = JsonConvert.DeserializeObject<BotSegment>(jsonFile.Item2);
                if (segment.EffectiveApiVersion > SdkApiVersion.CURRENT_VERSION)
                {
                    throw new Exception($"Bot segment file requires SDK version {segment.EffectiveApiVersion}, but the currently installed SDK version is {SdkApiVersion.CURRENT_VERSION}");
                }
               return new BotSegmentList(path, new List<BotSegment> {segment});
            }
            catch (Exception e)
            {
                throw new Exception($"Exception while parsing bot segment from resource path: {path}", e);
            }
        }

        /**
         * <summary>
         * Adds all segments from a folder path to this sequence.  This assumes that the path is a Unity Resource path to a folder containing bot segment json files.
         * If all files in the directory have numeric file names, then they will be ordered numerically.  Otherwise the files will be ordered lexicographically.
         * This API will only load .json files from the directory that are parsable as a BotSegment.
         * NOTE: This API imports the files in the directory as an immutable BotSegmentList.
         * </summary>
         * <exception>If any of the .json files in the directory are NOT parsable as a BotSegment, this API will throw an exception.</exception>
         */
        private void AddBotSegmentsFromFolderAsBotSegmentList(string path)
        {
            //TODO: Future ... We haven't found a way to get the resource names for the path in `LoadJsonResourcesFromDirectory` and thus cannot reliably order them.  Until we can, this API is disabled
        }

        // FUTURE: Unused for now, but used during parsing a directory listing
        private static BotSegmentList ParseDirectoryBotSegmentJsons(List<(string,string)> jsons, out string sessionId)
        {
            BotSegmentList result = new BotSegmentList();
            List<BotSegment> resultSegments = new();

            sessionId = null;
            var versionMismatch = -1;
            string badSegmentPath = null;
            try
            {
                foreach (var jsonFile in jsons)
                {
                    badSegmentPath = jsonFile.Item1;

                    var frameData = JsonConvert.DeserializeObject<BotSegment>(jsonFile.Item2);

                    if (frameData.EffectiveApiVersion > SdkApiVersion.CURRENT_VERSION)
                    {
                        versionMismatch = frameData.EffectiveApiVersion;
                        break;
                    }

                    if (sessionId == null)
                    {
                        sessionId = frameData.sessionId;
                    }

                    resultSegments.Add(frameData);
                }
            }
            catch (Exception e)
            {
                // Failed to parse the json.  End user doesn't really need this message, this is for developers at RG creating new bot segment types.. we give them a for real exception below
                throw new Exception($"Exception while parsing bot segment: {badSegmentPath}", e);
            }

            if (versionMismatch > 0)
            {
                throw new Exception($"Error parsing bot segment: {badSegmentPath}.  A segment requires SDK version {versionMismatch}, but the currently installed SDK version is {SdkApiVersion.CURRENT_VERSION}");
            }

            if (resultSegments.Count == 0)
            {
                // entries was empty
                throw new Exception($"Error parsing bot segments: [{string.Join(',',jsons.Select(e=>e.Item1))}].  All files must include a bot segment json.  Ensure you are loading valid bot segment resource paths.");
            }

            result.segments = resultSegments;

            return result;
        }
    }
}
