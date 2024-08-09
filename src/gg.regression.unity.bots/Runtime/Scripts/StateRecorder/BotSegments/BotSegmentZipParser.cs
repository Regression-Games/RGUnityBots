using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments
{
    public static class BotSegmentZipParser
    {

        public static List<BotSegment> ParseBotSegmentZipFromSystemPath(string zipFilePath, out string sessionId)
        {
            List<BotSegment> results = new();

            sessionId = null;

            using var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Read);

            // sort by numeric value of entries (not string comparison of filenames)
            var entries = zipArchive.Entries.Where(e => e.Name.EndsWith(".json")).OrderBy(e => int.Parse(e.Name.Substring(0, e.Name.IndexOf('.'))));

            var versionMismatch = -1;
            string badSegmentName = null;
            try
            {
                var replayNumber = 1;
                foreach (var entry in entries)
                {
                    using var sr = new StreamReader(entry.Open());
                    var frameData = JsonConvert.DeserializeObject<BotSegment>(sr.ReadToEnd());

                    if (frameData.EffectiveApiVersion > SdkApiVersion.CURRENT_VERSION)
                    {
                        versionMismatch = frameData.EffectiveApiVersion;
                        break;
                    }

                    badSegmentName = entry.FullName;

                    frameData.Replay_SegmentNumber = replayNumber++;

                    if (sessionId == null)
                    {
                        sessionId = frameData.sessionId;
                    }

                    results.Add(frameData);
                }
            }
            catch (Exception e)
            {
                // Failed to parse the json.  End user doesn't really need this message, this is for developers at RG creating new bot segment types.. we give them a for real exception below
                throw new Exception($"Exception while parsing bot segments zip: {zipFilePath}", e);
            }

            if (versionMismatch > 0)
            {
                throw new Exception($"Error parsing bot segments .zip at segment: {badSegmentName}.  File contains segments which require SDK version {versionMismatch}, but currently installed SDK version is {SdkApiVersion.CURRENT_VERSION}");
            }

            if (results.Count == 0)
            {
                // entries was empty
                throw new Exception($"Error parsing bot segments .zip at segment: {badSegmentName}.  Must include at least 1 bot segment json.  Ensure you are loading a valid 'bot_segments.zip' file.");
            }

            return results;
        }
    }
}
