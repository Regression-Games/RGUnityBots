using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments.Models;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.StateRecorder
{

    public class ReplayBotSegmentsContainer
    {
        private readonly List<BotSegment> _botSegments = new();
        private int _botSegmentIndex = 0;

        public string SessionId { get; private set; }
        public bool IsShiftDown;

        public ReplayBotSegmentsContainer(string zipFilePath)
        {
            ParseReplayZip(zipFilePath);
        }

        public void Reset()
        {
            // sets indexes back to 0
            _botSegmentIndex = 0;

            IsShiftDown = false;

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

            // sort by numeric value of entries (not string comparison of filenames)
            var entries = zipArchive.Entries.Where(e => e.Name.EndsWith(".json")).OrderBy(e => int.Parse(e.Name.Substring(0, e.Name.IndexOf('.'))));

            var bsCount = _botSegments.Count;

            try
            {
                var replayNumber = 1;
                foreach (var entry in entries)
                {
                    using var sr = new StreamReader(entry.Open());
                    var frameData = JsonConvert.DeserializeObject<BotSegment>(sr.ReadToEnd());

                    frameData.Replay_Number = replayNumber++;

                    if (SessionId == null)
                    {
                        SessionId = frameData.sessionId;
                    }

                    _botSegments.Add(frameData);
                }
            }
            catch (Exception)
            {
                // Failed to parse the json.  End user doesn't really need this message.. we give them a for real exception below
            }

            if (_botSegments.Count == bsCount)
            {
                // entries was empty
                throw new Exception("Error parsing bot segments .zip.  Must include at least 1 bot segment json.  Ensure you are loading a 'bot_segments.zip' file.");
            }
        }
    }
}
