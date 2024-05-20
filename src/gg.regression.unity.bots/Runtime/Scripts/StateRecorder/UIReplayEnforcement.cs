namespace StateRecorder
{
    public enum UIReplayEnforcement
    {
        /**
         * <summary>UI elements must exactly match the UI elements in the recording, no extra, no less.  This can be problematic for games that load in cards on timers or other aspects where the frame by frame results aren't perfectly deterministic each run.</summary>
         */
        Strict,
        /**
         * <summary>Considers non exact delta states from prior key frame.  Looks for counts going to 0, increasing, decreasing, or being non-zero.</summary>
         */
        Delta,
        /**
         * <summary>Just play the inputs based on their timings without validating UI elements at all.  Not recommended.</summary>
         */
        None
    }
}
