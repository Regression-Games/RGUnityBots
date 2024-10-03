using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
// ReSharper disable once RedundantUsingDirective - used in #if block
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models
{

    /**
     * <summary>Used to define a sequence of BotSegment/BotSegmentList as a single bot.  This is used to load bot sequences from json or to build a new sequence using the UI</summary>
     */
    [Serializable]
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
            if (string.IsNullOrEmpty(path))
            {
                return (null, null, null);
            }

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

            return (sequenceJson.Item1, sequenceJson.Item2, JsonConvert.DeserializeObject<BotSequence>(sequenceJson.Item3, JsonUtils.JsonSerializerSettings));
        }

        /**
         * <summary>
         * Parses the specified BotSegment or BotSegmentList from the json at the given path.  This assumes that the path is a Unity Resource path to a BotSegment or BotSegmentList json file.
         * </summary>
         * <exception>If the .json path specified is NOT parsable as a BotSegment or BotSegmentList, this API will throw an exception.</exception>
         * <returns>The parsed BotSegmentList or a BotSegmentList with the single BotSegment Found</returns>
         */
        private static BotSegmentList CreateBotSegmentListForPath(string path, out string sessionId)
        {
            var result = LoadBotSegmentOrBotSegmentListFromPath(path);
            if (result.Item3 is BotSegmentList bsl)
            {
                sessionId = bsl.segments.FirstOrDefault(a => !string.IsNullOrEmpty(a.sessionId))?.sessionId;
                return bsl;
            }
            else
            {
                var segment = (BotSegment)result.Item3;
                sessionId = segment.sessionId;
                var segmentList = new BotSegmentList(path, new List<BotSegment> { segment })
                {
                    name = segment.name,
                    resourcePath = segment.resourcePath,
                    description = segment.description
                };
                segmentList.FixupNames();
                return segmentList;
            }
        }

        private static object ParseSegmentOrListJson(string resourcePath, string fileData)
        {
            try
            {
                // this check is still way faster than running the json parser
                if (!fileData.Contains("\"segments\":"))
                {
                    throw new Exception("Not a segment list");
                }
                var segmentList = JsonConvert.DeserializeObject<BotSegmentList>(fileData, JsonUtils.JsonSerializerSettings);
                if (segmentList.EffectiveApiVersion > SdkApiVersion.CURRENT_VERSION)
                {
                    throw new Exception($"BotSegmentList file contains a segment which requires SDK version {segmentList.EffectiveApiVersion}, but the currently installed SDK version is {SdkApiVersion.CURRENT_VERSION}");
                }

                segmentList.resourcePath = resourcePath;

                segmentList.FixupNames();
                return segmentList;

            }
            catch (Exception)
            {
                // This wasn't a segment list, so it must be a normal segment
                var segment = JsonConvert.DeserializeObject<BotSegment>(fileData, JsonUtils.JsonSerializerSettings);
                if (segment.EffectiveApiVersion > SdkApiVersion.CURRENT_VERSION)
                {
                    throw new Exception($"BotSegment file requires SDK version {segment.EffectiveApiVersion}, but the currently installed SDK version is {SdkApiVersion.CURRENT_VERSION}");
                }

                segment.resourcePath = resourcePath;
                return segment;
            }
        }

        /**
         *
         * <returns>A (FilePath [null - if loaded as resource], resourcePath, object [BotSegment or BotSegmentList]) tuple</returns>
         */
        public static (string, string, object) LoadBotSegmentOrBotSegmentListFromPath(string path)
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

                return (jsonFile.Item1, jsonFile.Item2, ParseSegmentOrListJson(jsonFile.Item2, jsonFile.Item3));
            }
            catch (Exception e)
            {
                throw new Exception($"Exception while parsing BotSegment or BotSegmentList from resource path: {path}", e);
            }
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
            StringJsonConverter.WriteToStringBuilder(stringBuilder, name);
            stringBuilder.Append(",\n\"description\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, description);
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

        private string SequencePathName
        {
            get
            {
                var filepath = string.Join("-", name.Split(" "));
                foreach (var c in Path.GetInvalidPathChars())
                {
                    filepath = filepath.Replace(c, '-');
                }
                return filepath;
            }
        }

        /**
         * <summary>In the Unity Editor this will create a new resource under "Assets/RegressionGames/Resources/BotSequences".  In runtime builds, it will write to "{Application.persistentDataPath}/RegressionGames/Resources/BotSequences" .</summary>
         */
        public void SaveSequenceAsJson()
        {
            try
            {
                string directoryPath = null;
#if UNITY_EDITOR
                directoryPath = "Assets/RegressionGames/Resources/BotSequences";
#else
                directoryPath = Application.persistentDataPath + "/RegressionGames/Resources/BotSequences";
#endif
                Directory.CreateDirectory(directoryPath);

                var filepath = directoryPath + "/" + SequencePathName + ".json";

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
         * Useful for copying segments from a sequence all to a new path.  This will update the path references of this sequence accordingly.
         */
        public void CopySequenceSegmentsToNewPath()
        {
            var segmentDataList = new List<((string, string, object), BotSequenceEntry)>();
            // load them all into ram first so we can delete the directory safely.. this is in case the source and destination happen to be the same for some segments
            foreach (var botSequenceEntry in segments)
            {
                segmentDataList.Add((LoadBotSegmentOrBotSegmentListFromPath(botSequenceEntry.path), botSequenceEntry));
            }

            // ReSharper disable once JoinDeclarationAndInitializer - #if clauses
            string directoryPath;
#if UNITY_EDITOR
            directoryPath = "Assets/RegressionGames/Resources/BotSegments/" + SequencePathName;
#else
            directoryPath = Application.persistentDataPath + "/RegressionGames/Resources/BotSegments/" + SequencePathName;
#endif

            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }

            Directory.CreateDirectory(directoryPath);

            foreach (var segmentData in segmentDataList)
            {
                var segment = segmentData.Item1;
                var botSequenceEntry = segmentData.Item2;

                var filename = segment.Item2.Replace('\\', '/');
                var index = filename.LastIndexOf('/');
                if (index >= 0)
                {
                    filename = filename.Substring(index+1);
                }

                if (!filename.EndsWith(".json"))
                {
                    filename += ".json";
                }

                var filePath = directoryPath + "/" + filename;
                botSequenceEntry.path = filePath;

                RGDebug.LogDebug($"Copying segment from: {segment.Item1 ?? segment.Item2} , to: {filePath}");
                using var sw = File.CreateText(filePath);
                if (segment.Item3 is BotSegment botSegment)
                {
                    sw.Write(botSegment.ToJsonString());
                }
                else if (segment.Item3 is BotSegmentList botSegmentList)
                {
                    sw.Write(botSegmentList .ToJsonString());
                }
                sw.Close();

            }
        }

        /**
         * <summary>
         * Checks if there exists a file at the path param, and deletes the file if so.
         * </summary>
         * <para name="path">The Sequence path to delete</para>
         */
        public static void DeleteSequenceAtPath(string path)
        {
            // load the resource to get correct pathing information
            var resource = LoadJsonResource(path);

            if (!string.IsNullOrEmpty(resource.Item1))
            {
                if (!File.Exists(resource.Item1))
                {
                    RGDebug.LogError($"BotSequence at: {resource.Item1} cannot be deleted... path does not exist");
                }
                else
                {
                    try
                    {
                        File.Delete(resource.Item1);
                        // attempt to remove unity's meta file for this if it exists
                        var metaPath = resource.Item1 + ".meta";
                        if (File.Exists(metaPath))
                        {
                            File.Delete(metaPath);
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Exception trying to delete BotSequence at: {resource.Item1}", e);
                    }
                }
            }
            else
            {
                RGDebug.LogError($"BotSequence at path: {resource.Item2} cannot be deleted... BotSequence was loaded as a Resource.  Remove that BotSequence json file from your project's Assets if you wish to permanently delete it.");
            }

        }

        public static string ToResourcePath(string path)
        {
            var resourcePath = path;
            if (resourcePath.Contains("Resources/"))
            {
                resourcePath = resourcePath.Substring(resourcePath.LastIndexOf("Resources/") + "Resources/".Length);
            }

            var lastIndex = resourcePath.LastIndexOf(".json");
            if (lastIndex >= 0)
            {
                resourcePath = resourcePath.Substring(0, lastIndex);
            }

            return resourcePath;
        }

        /**
         * <summary>Load the json resource at the specified path.  If .json is not on this path it will be auto appended.</summary>
         * <returns>A (FilePath [null - if loaded as resource], resourcePath, contentString) tuple</returns>
         */
        public static (string, string, string) LoadJsonResource(string path)
        {
            (string, string, string)? result = null;

            var resourcePath = ToResourcePath(path);

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
                if (File.Exists(Application.persistentDataPath + "/RegressionGames/Resources/" + runtimePath))
                {
                    using var fr = new StreamReader(File.OpenRead(Application.persistentDataPath + "/RegressionGames/Resources/" + runtimePath));
                    result = (Application.persistentDataPath + "/RegressionGames/Resources/" + runtimePath, resourcePath, fr.ReadToEnd());
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Exception reading json files from directory: {Application.persistentDataPath + "/RegressionGames/Resources/" + path}", e);
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

        /**
         * <summary>Run the defined bot segments</summary>
         */
        public void Play()
        {
            string sessionId = null;
            _segmentsToProcess.Clear();
            foreach (var segmentEntry in segments)
            {
                _segmentsToProcess.Add(CreateBotSegmentListForPath(segmentEntry.path, out var sessId));
                sessionId ??= sessId;
            }

            sessionId ??= Guid.NewGuid().ToString();

            var playbackController = UnityEngine.Object.FindObjectOfType<BotSegmentsPlaybackController>();
            playbackController.Stop();
            playbackController.Reset();
            playbackController.SetDataContainer(new BotSegmentsPlaybackContainer(_segmentsToProcess.SelectMany(a => a.segments), sessionId));
            playbackController.Play();
        }

        public void Stop()
        {
            var playbackController = UnityEngine.Object.FindObjectOfType<BotSegmentsPlaybackController>();
            playbackController.Stop();
            playbackController.Reset();
        }

        public static BotSequenceEntry CreateBotSequenceEntryForJson(string filePath, string resourcePath, string jsonData)
        {
            try
            {
                var result = ParseSegmentOrListJson(resourcePath, jsonData);
                if (result is BotSegmentList bsl)
                {
                    return new BotSequenceEntry()
                    {
                        type = BotSequenceEntryType.SegmentList,
                        path = filePath ?? resourcePath,
                        resourcePath = resourcePath,
                        name = bsl.name,
                        description = bsl.description
                    };
                }

                var botSegment = (BotSegment)result;
                return new BotSequenceEntry()
                {
                    type = BotSequenceEntryType.Segment,
                    path = filePath ?? resourcePath,
                    resourcePath = resourcePath,
                    name = botSegment.name,
                    description = botSegment.description
                };

            }
            catch (Exception e)
            {
                RGDebug.LogWarning($"Exception parsing BotSegmentList or BotSegment from json at path: {resourcePath} - {e}");
            }

            return null;
        }

        /**
         * <returns>A (FilePath [null - if loaded as resource], resourcePath, BotSequenceEntry) tuple</returns>
         */
        public static (string,string,BotSequenceEntry) CreateBotSequenceEntryForPath(string path)
        {
            try
            {
                var result = LoadBotSegmentOrBotSegmentListFromPath(path);
                if (result.Item3 is BotSegmentList bsl)
                {
                    return (result.Item1, result.Item2, new BotSequenceEntry()
                    {
                        type = BotSequenceEntryType.SegmentList,
                        path = result.Item1 ?? result.Item2,
                        resourcePath = result.Item2,
                        name = bsl.name,
                        description = bsl.description
                    });
                }

                var botSegment = (BotSegment)result.Item3;
                return (result.Item1, result.Item2, new BotSequenceEntry()
                {
                    type = BotSequenceEntryType.Segment,
                    path = result.Item1 ?? result.Item2,
                    resourcePath = result.Item2,
                    name = botSegment.name,
                    description = botSegment.description
                });

            }
            catch (Exception e)
            {
                RGDebug.LogWarning($"Exception parsing BotSegmentList or BotSegment at path: {path} - {e}");
            }

            return (null,null,null);
        }

        /**
         * <summary>
         * Adds the specified BotSegmentList or BotSegment to this sequence. This assumes that the path is a path to a valid BotSegmentList or BotSegment json file.
         * </summary>
         * <returns>true if entry added, false if path doesn't exist or parsing error occurs</returns>
         */
        public bool AddSequenceEntryForPath(string path)
        {
            var entry = CreateBotSequenceEntryForPath(path);
            if (entry.Item3 != null)
            {
                segments.Add(entry.Item3);
                return true;
            }
            return false;
        }

    }
}
