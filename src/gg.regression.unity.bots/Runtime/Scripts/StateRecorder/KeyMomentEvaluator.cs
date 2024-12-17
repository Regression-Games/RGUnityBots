
using System.Collections.Generic;
using JetBrains.Annotations;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.KeyMoments;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder
{
    public class KeyMomentEvaluator
    {

        private long _keyMomentNumber;

        public void Reset()
        {
            _keyMomentNumber = 1;
        }

        // Future: We may change this to allow easier registration of evaluators by scanning the classpath... we already do a pattern like this in other code in the SDK, so should be easy to add in later
        // Most of this class is where future work for new recording of key moments for keyboard inputs or actions will be glued in.  This class WILL change its implementation quite a bit as those pieces are added and we see what patterns work best.
        private readonly IKeyMomentEvaluator[] _keyMomentEvaluators = {
            new MouseKeyMomentEvaluator()
        };

        [CanBeNull]
        public BotSegment EvaluateKeyMoment(long tickNumber, out long keyMomentNumber)
        {
            // Future: This currently considers that only 1 key moment action will be return per segment.  Maybe this is fine, but we may need to handle returning multiple segments for a single pass at some point,
            // or maybe better would be smashing these into a BotSegmentList.  Since it would be ambiguous to try to replay N actions at the same time, these will have to be sequenced in some way, OR we'll need to create
            // evaluator/action-class pairs that blend things as we need.  (ie: Mouse/Keyboard interleaving, Mouse/GameAction interleaving, etc)  TBD if this is a real thing or just a possibility being considered.
            keyMomentNumber = _keyMomentNumber;
            BotSegment result = null;
            foreach (var keyMomentEvaluator in _keyMomentEvaluators)
            {
                var segment = keyMomentEvaluator.IsKeyMoment(tickNumber, _keyMomentNumber);
                if (segment != null)
                {
                    result = segment;
                }
            }

            if (result != null)
            {
                keyMomentNumber = _keyMomentNumber;
                // increment after reporting in the result
                ++_keyMomentNumber;
            }

            return result;
        }

        public void UpdateMouseInputData(List<MouseInputActionData> mouseInputData)
        {
            foreach (var keyMomentEvaluator in _keyMomentEvaluators)
            {
                if (keyMomentEvaluator is MouseKeyMomentEvaluator mouseKeyMomentEvaluator)
                {
                    mouseKeyMomentEvaluator.MouseDataBuffer.AddRange(mouseInputData);
                    break;
                }
            }
        }
    }
}
