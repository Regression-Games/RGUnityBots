using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using StateRecorder.BotSegments.Models;
using StateRecorder.BotSegments.Models.SegmentValidations;

namespace RegressionGames.StateRecorder.BotSegments
{

    public class BotSegmentsPlaybackContainer
    {
        private readonly List<BotSegment> _botSegments;
        private int _botSegmentIndex = 0;
        
        /**
         * A top-level set of validations to run for an entire sequence of segments
         */
        public readonly List<SegmentValidation> Validations;

        public readonly string SessionId;

        public BotSegmentsPlaybackContainer(IEnumerable<BotSegment> segments, IEnumerable<SegmentValidation> validations, string sessionId = null)
        {
            var replayNumber = 1; // 1 to align with the actual numbers in the recording
            _botSegments = new(segments);
            Validations = new(validations);
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
            
            // reset all the top-level validations
            foreach (var validation in Validations)
            {
                validation.ReplayReset();
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
        
        
        /**
         * <summary>Collects all of the results from the top-level validations and individual bot segments</summary>
         */
        public List<SegmentValidationResultSetContainer> GetAllValidationResults()
        {
            
            // First add all the top level results
            var results = Validations.Select(validation => validation.data.GetResults()).ToList();

            // Then add the individual bot segment results
            foreach (var botSegment in _botSegments)
            {
                results.AddRange(botSegment.validations.Select(v => v.data.GetResults()));
            }

            return results;
        }
        
    }
}
