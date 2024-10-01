using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using RegressionGames;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments.Models;
using UnityEngine;

namespace StateRecorder.BotSegments
{
    public static class BotSegmentDirectoryParser
    {

        /**
         * <summary>This API assumes that the resourcePath is a unity resource path</summary>
         */
        public static List<BotSegment> ParseBotSegmentResourcePath(string resourcePath, out string sessionId)
        {
            List<BotSegment> results = new();

            sessionId = null;
            var versionMismatch = -1;
            try
            {
                var jsons = Resources.LoadAll(resourcePath, typeof(TextAsset));

                // TODO: Can we figure out some way to sort these by name even though unity doesn't expose the resource name ?
                foreach (var entry in jsons)
                {
                    var jsonFile = entry as TextAsset;
                    var frameData = JsonConvert.DeserializeObject<BotSegment>(jsonFile.text, JsonUtils.JsonSerializerSettings);

                    if (frameData.EffectiveApiVersion > SdkApiVersion.CURRENT_VERSION)
                    {
                        versionMismatch = frameData.EffectiveApiVersion;
                        break;
                    }

                    if (sessionId == null)
                    {
                        sessionId = frameData.sessionId;
                    }

                    results.Add(frameData);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Exception while parsing bot segments resource path: {resourcePath}", e);
            }

            if (versionMismatch > 0)
            {
                throw new Exception($"Error parsing bot segments resource path: {resourcePath}.  A file contains a segment which requires SDK version {versionMismatch}, but the currently installed SDK version is {SdkApiVersion.CURRENT_VERSION}");
            }

            if (results.Count == 0)
            {
                // entries was empty
                throw new Exception($"Error parsing bot segments resource path: {resourcePath}.  Each file must include at least 1 bot segment json.  Ensure you are loading a valid bot segments resource path.");
            }

            return results;
        }

        /**
         * <summary>Sort json files FROM THE SAME DIRECTORY numerically if possible, otherwise Lexicographically</summary>
         */
        public static IOrderedEnumerable<string> OrderJsonFiles(IEnumerable<string> jsonFiles)
        {
            RGDebug.LogInfo($"OrderJsonFiles - Input File List - {string.Join("\n", jsonFiles)}");
            Exception lambdaException = null;
            // sort by numeric value of entries (not string comparison of filenames) .. normalize to front slashes
            jsonFiles = jsonFiles.Where(e=>e.EndsWith(".json")).Select(e =>e = e.Replace('\\', '/'));
            IOrderedEnumerable<string> entries;
            try
            {
                // try to order the files as numbered file names
                entries = jsonFiles.OrderBy(e =>
                {
                    if (lambdaException != null)
                    {
                        // avoid parsing the rest...
                        return 0;
                    }
                    try
                    {
                        // trim off the path for numeric sorting evaluation
                        var i = e.LastIndexOf('/');
                        var subE = e;
                        if (i >= 0)
                        {
                            subE = subE.Substring(i+1);

                        }
                        subE = subE.Substring(0, subE.IndexOf('.'));
                        return int.Parse(subE);
                    }
                    catch (Exception le)
                    {
                        lambdaException = le;
                    }
                    return 0;
                });
                if (lambdaException != null)
                {
                    throw lambdaException;
                }
            }
            catch (Exception)
            {
                // if filenames aren't all numbers, order lexicographically
                entries = jsonFiles.OrderBy(e => e.Substring(0, e.IndexOf('.')));
            }

            RGDebug.LogInfo($"OrderJsonFiles - Output File List - {string.Join("\n", entries)}");
            return entries;
        }

        public static List<BotSegment> ParseBotSegmentSystemDirectory(string directoryPath, out string sessionId)
        {
            List<BotSegment> results = new();

            sessionId = null;

            var versionMismatch = -1;
            string badSegmentName = null;
            try
            {
                var filesInDirectory = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);

                IOrderedEnumerable<string> entries = OrderJsonFiles(filesInDirectory);

                foreach (var entry in entries)
                {
                    using var sr = new StreamReader(File.OpenRead(entry));
                    var frameData = JsonConvert.DeserializeObject<BotSegment>(sr.ReadToEnd(), JsonUtils.JsonSerializerSettings);

                    if (frameData.EffectiveApiVersion > SdkApiVersion.CURRENT_VERSION)
                    {
                        versionMismatch = frameData.EffectiveApiVersion;
                        break;
                    }

                    badSegmentName = entry;

                    if (sessionId == null)
                    {
                        sessionId = frameData.sessionId;
                    }

                    results.Add(frameData);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Exception while parsing bot segments directory: {directoryPath}", e);
            }

            if (versionMismatch > 0)
            {
                throw new Exception($"Error parsing bot segments directory at segment: {badSegmentName}.  File contains segments which require SDK version {versionMismatch}, but currently installed SDK version is {SdkApiVersion.CURRENT_VERSION}");
            }

            if (results.Count == 0)
            {
                // entries was empty
                throw new Exception($"Error parsing bot segments directory at segment: {badSegmentName}.  Must include at least 1 bot segment json.  Ensure you are loading a valid bot segments directory path.");
            }

            return results;
        }
    }
}
