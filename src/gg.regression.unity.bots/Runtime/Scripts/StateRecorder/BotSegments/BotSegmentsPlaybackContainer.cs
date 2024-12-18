using System;
using System.Collections.Generic;
using RegressionGames.StateRecorder.BotSegments.Models;
using StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments
{

    public class BotSegmentsPlaybackContainer
    {

        private readonly List<BotSegmentList> _botSegmentLists;
        private int _botSegmentListIndex = 0;
        private int _botSegmentIndex = 0;

        /**
         * A top-level set of validations to run for an entire sequence of segments
         */
        public readonly List<SegmentValidation> Validations;

        public readonly string SessionId;

        public BotSegmentsPlaybackContainer(IEnumerable<BotSegmentList> segmentLists, IEnumerable<SegmentValidation> validations, string sessionId = null)
        {
            var replayNumber = 1; // 1 to align with the actual numbers in the recording
            _botSegmentLists = new(segmentLists);
            _botSegmentLists.ForEach(a => a.segments.ForEach(b => b.Replay_SegmentNumber = replayNumber++));
            Validations = new(validations);
            this.SessionId = sessionId ?? Guid.NewGuid().ToString("n");
        }

        public void Reset()
        {
            // sets indexes back to 0
            _botSegmentListIndex = 0;

            // reset all the tracking flags in the segmentlists / segments
            _botSegmentLists.ForEach(a =>
            {
                a.segments.ForEach(b => b.ReplayReset());
                a.validations.ForEach(b => b.ReplayReset());
            });

            // reset all the top-level validations
            foreach (var validation in Validations)
            {
                validation.ReplayReset();
            }

        }

        /**
         * Returns the next bot segment to evaluate and also provides the current segmentList level validations
         */
        public BotSegment DequeueBotSegment(out List<SegmentValidation> segmentListValidations)
        {
            while (_botSegmentListIndex < _botSegmentLists.Count)
            {
                var segmentList = _botSegmentLists[_botSegmentListIndex];
                if (_botSegmentIndex < segmentList.segments.Count)
                {
                    var segment = segmentList.segments[_botSegmentIndex++];
                    segmentListValidations = segmentList.validations;
                    return segment;
                }
                else
                {
                    // move to the next segmentlist starting on the 0th segment in that list
                    _botSegmentIndex = 0;
                    ++_botSegmentListIndex;
                }

            }

            segmentListValidations = new List<SegmentValidation>();
            return null;
        }
    }
}
