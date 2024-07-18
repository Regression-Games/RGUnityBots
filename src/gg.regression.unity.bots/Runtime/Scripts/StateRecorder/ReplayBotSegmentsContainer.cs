using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder
{

    public class ReplayBotSegmentsContainer
    {
        private readonly List<BotSegment> _botSegments = new();
        private int _botSegmentIndex = 0;

        public string SessionId { get; private set; }

        public ReplayBotSegmentsContainer(string zipFilePath)
        {
            ParseReplayZip(zipFilePath);
        }

        public void Reset()
        {
            // sets indexes back to 0
            _botSegmentIndex = 0;

            // reset all the tracking flags
            foreach (var botSegment in _botSegments)
            {
                botSegment.ReplayReset();
            }
        }

        public BotSegment DequeueBotSegment()
        {
            if (_botSegmentIndex < _botSegments.Count)
            {
                return _botSegments[_botSegmentIndex++];
            }

            return null;
        }

        public BotSegment PeekBotSegment()
        {
            if (_botSegmentIndex < _botSegments.Count)
            {
                // do not update index
                return _botSegments[_botSegmentIndex];
            }

            return null;
        }

        public void ParseReplayZip(string zipFilePath)
        {
            using var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Read);

            // if the zip contains only one entry that is a directory, dig into that before iterating files (helps handle OS zipped directory formats)



            // sort by numeric value of entries (not string comparison of filenames)
            var entries = zipArchive.Entries.Where(e => e.Name.EndsWith(".json")).OrderBy(e => int.Parse(e.Name.Substring(0, e.Name.IndexOf('.'))));

            var bsCount = _botSegments.Count;

            var versionMismatch = -1;
            var badSegmentNumber = 1; // keep this to current entry count ahead of time so we know which one failed
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

                    ++badSegmentNumber;

                    frameData.Replay_SegmentNumber = replayNumber++;

                    if (SessionId == null)
                    {
                        SessionId = frameData.sessionId;
                    }

                    _botSegments.Add(frameData);
                }
            }
            catch (Exception e)
            {
                // Failed to parse the json.  End user doesn't really need this message, this is for developers at RG creating new bot segment types.. we give them a for real exception below
                RGDebug.LogWarning("Exception while parsing bot_segments.zip - " + e);
            }

            if (versionMismatch > 0)
            {
                throw new Exception($"Error parsing bot segments .zip at segment #{badSegmentNumber}.  File contains segments which require SDK version {versionMismatch}, but currently installed SDK version is {SdkApiVersion.CURRENT_VERSION}");
            }

            if (_botSegments.Count == bsCount)
            {
                // entries was empty
                throw new Exception($"Error parsing bot segments .zip at segment #{badSegmentNumber}.  Must include at least 1 bot segment json.  Ensure you are loading a 'bot_segments.zip' file.");
            }
        }
    }
}
