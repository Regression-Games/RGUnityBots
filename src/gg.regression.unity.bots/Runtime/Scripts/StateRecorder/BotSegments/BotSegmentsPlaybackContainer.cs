﻿using System;
using System.Collections.Generic;
using RegressionGames.StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments
{

    public class BotSegmentsPlaybackContainer
    {
        private readonly List<BotSegment> _botSegments = new();
        private int _botSegmentIndex = 0;

        public readonly string SessionId;

        public BotSegmentsPlaybackContainer(IEnumerable<BotSegment> segments, string sessionId = null)
        {
            _botSegments.Clear();
            _botSegments.AddRange(segments);
            this.SessionId = sessionId ?? Guid.NewGuid().ToString("n");
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
    }
}