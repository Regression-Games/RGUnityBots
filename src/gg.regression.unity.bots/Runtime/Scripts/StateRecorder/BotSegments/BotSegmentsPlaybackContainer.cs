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
                a.segments.ForEach(b => b.ReplayReset()); // This also handles resetting that segments validations
                a.validations.ForEach(b => b.ReplayReset());
            });

            // reset all the top-level sequence validations
            foreach (var validation in Validations)
            {
                validation.ReplayReset();
            }

        }

        /**
         * Returns the next bot segment and validations to evaluate and also provides the current segmentList level validations
         */
        public (BotSegment, List<SegmentValidation>)? DequeueBotSegment()
        {
            while (_botSegmentListIndex < _botSegmentLists.Count)
            {
                var segmentList = _botSegmentLists[_botSegmentListIndex];
                if (_botSegmentIndex < segmentList.segments.Count)
                {
                    var segment = segmentList.segments[_botSegmentIndex++];
                    var segmentListValidations = segmentList.validations;
                    return (segment, segmentListValidations);
                }
                else
                {
                    // move to the next segmentlist starting on the 0th segment in that list
                    _botSegmentIndex = 0;
                    ++_botSegmentListIndex;
                }

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

            // Then add the validations from bot segment lists and individual bot segment results
            foreach (var botSegmentList in _botSegmentLists)
            {
                results.AddRange(botSegmentList.validations.Select(v => v.data.GetResults()));
                
                foreach (var botSegment in botSegmentList.segments)
                {
                    results.AddRange(botSegment.validations.Select(v => v.data.GetResults()));
                }
            }

            return results;
        }

        /**
         * <summary>
         * This will request to stop all validations in the container, including sequence validations, bot
         * segment list validations, and individual bot segment validations.
         * </summary>
         */
        public void StopAllValidations(int segmentNumber)
        {
            // First stop the sequence validations
            foreach (var validation in Validations)
            {
                validation.StopValidation(segmentNumber);
            }
            
            // Then stop the segment list validations and bot segment validations
            foreach (var botSegmentList in _botSegmentLists)
            {
                botSegmentList.validations.ForEach(v => v.StopValidation(segmentNumber));
                foreach (var botSegment in botSegmentList.segments)
                {
                    botSegment.validations.ForEach(v => v.StopValidation(segmentNumber));
                }
            }
        }
        
    }
}
