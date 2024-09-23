using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments
{

    public class BotSegmentsPlaybackContainer
    {
        private readonly List<BotSegment> _botSegments;
        private int _botSegmentIndex = 0;

        public readonly string SessionId;

        public BotSegmentsPlaybackContainer(IEnumerable<BotSegment> segments, string sessionId = null)
        {
            var replayNumber = 1; // 1 to align with the actual numbers in the recording
            _botSegments = new(segments);
            _botSegments.ForEach(a => a.Replay_SegmentNumber = replayNumber++);
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
