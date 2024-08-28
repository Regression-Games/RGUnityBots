using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.BotSegments.Models;
using StateRecorder.BotSegments;

namespace RegressionGames.StateRecorder.BotSegments
{
    public static class BotSegmentZipParser
    {

        public static IOrderedEnumerable<ZipArchiveEntry> OrderZipJsonEntries(IEnumerable<ZipArchiveEntry> jsonFiles)
        {
            RGDebug.LogInfo($"OrderJsonFiles - Input File List - {string.Join("\n", jsonFiles.Select(a=>a.Name))}");
            Exception lambdaException = null;
            // sort by numeric value of entries (not string comparison of filenames) .. normalize to front slashes
            jsonFiles = jsonFiles.Where(e=>e.Name.EndsWith(".json"));
            IOrderedEnumerable<ZipArchiveEntry> entries;
            try
            {
                // try to order the files as numbered file names
                entries = jsonFiles.OrderBy(e =>
                {
                    var eName = e.Name.Replace('\\', '/');
                    if (lambdaException != null)
                    {
                        // avoid parsing the rest...
                        return 0;
                    }
                    try
                    {
                        // trim off the path for numeric sorting evaluation
                        var i = eName.LastIndexOf('/');
                        var subE = eName;
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
                entries = jsonFiles.OrderBy(e => e.Name.Substring(0, e.Name.IndexOf('.')));
            }

            RGDebug.LogInfo($"OrderJsonFiles - Output File List - {string.Join("\n", entries.Select(a=>a.Name))}");
            return entries;
        }

        public static List<BotSegment> ParseBotSegmentZipFromSystemPath(string zipFilePath, out string sessionId)
        {
            List<BotSegment> results = new();

            sessionId = null;

            using var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Read);

            // sort by numeric value of entries (not string comparison of filenames)
            var entries = OrderZipJsonEntries(zipArchive.Entries);

            var versionMismatch = -1;
            string badSegmentName = null;
            try
            {
                var replayNumber = 1;
                foreach (var entry in entries)
                {
                    using var sr = new StreamReader(entry.Open());

                    var fileText = sr.ReadToEnd();
                    badSegmentName = entry.FullName;

                    try
                    {
                        // see if this is a bot segment list file
                        var botSegmentList = JsonConvert.DeserializeObject<BotSegmentList>(fileText);

                        if (botSegmentList.EffectiveApiVersion > SdkApiVersion.CURRENT_VERSION)
                        {
                            versionMismatch = botSegmentList.EffectiveApiVersion;
                            break;
                        }

                        foreach (var botSegment in botSegmentList.segments)
                        {
                            botSegment.Replay_SegmentNumber = replayNumber++;

                            if (sessionId == null)
                            {
                                sessionId = botSegment.sessionId;
                            }

                            results.Add(botSegment);
                        }
                    }
                    catch (Exception)
                    {
                        try
                        {
                            // not a bot segment list, must be a regular segment
                            var frameData = JsonConvert.DeserializeObject<BotSegment>(fileText);

                            if (frameData.EffectiveApiVersion > SdkApiVersion.CURRENT_VERSION)
                            {
                                versionMismatch = frameData.EffectiveApiVersion;
                                break;
                            }

                            frameData.Replay_SegmentNumber = replayNumber++;

                            if (sessionId == null)
                            {
                                sessionId = frameData.sessionId;
                            }

                            results.Add(frameData);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Invalid/missing file data in BotSegment or BotSegmentList", ex);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Exception parsing bot segments zip: {zipFilePath} at segment: {badSegmentName}", e);
            }

            if (versionMismatch > 0)
            {
                throw new Exception($"Error parsing bot segments zip: {zipFilePath} at segment: {badSegmentName}.  File contains segments which require SDK version {versionMismatch}, but currently installed SDK version is {SdkApiVersion.CURRENT_VERSION}");
            }

            if (results.Count == 0)
            {
                // entries was empty
                throw new Exception($"Error parsing bot segments zip: {zipFilePath} at segment: {badSegmentName}.  Must include at least 1 bot segment json.  Ensure you are loading a valid 'bot_segments.zip' file.");
            }

            return results;
        }
    }
}
