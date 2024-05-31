﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using StateRecorder;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.StateRecorder
{

    public class ReplayBotSegmentsContainer
    {
        private readonly List<BotSegmment> _botSegments = new();
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

        public BotSegmment DequeueBotSegment()
        {
            if (_botSegmentIndex < _botSegments.Count)
            {
                return _botSegments[_botSegmentIndex++];
            }

            return null;
        }

        public BotSegmment PeekBotSegment()
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

            foreach (var entry in entries)
            {
                using var sr = new StreamReader(entry.Open());
                var frameData = JsonConvert.DeserializeObject<BotSegmment>(sr.ReadToEnd());

                if (SessionId == null)
                {
                    SessionId = frameData.sessionId;
                }

                _botSegments.Add(frameData);
            }

            if (_botSegments.Count < 1)
            {
                // entries was empty
                throw new Exception("Error parsing replay .zip.  Must include at least 1 frame json/jpg pair.");
            }
        }
    }
}
