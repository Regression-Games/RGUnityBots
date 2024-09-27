namespace RegressionGames.StateRecorder.Types
{
    /**
     * <summary>An interface that MonoBehaviours used as actions in RG BotSegments MAY implement to provide pause / un-pause functionality during Bot Segment evaluation.
     * Implementing this interface is not required for the behaviour action to run, only for it to support pause / un-pause.</summary>
     */
    public interface IRGBotSegmentActionBehaviour
    {
        /**
         * <summary>Called by Regression Games Bot Segment processing to indicate that the user has paused the playback of the bot segments.
         * This should normally be implemented by setting a boolean to true and checking that boolean at the start of your Update loop to block execution when paused.</summary>
         */
        public void PauseAction();

        /**
         * <summary>Called by Regression Games Bot Segment processing to indicate that the user has un-paused the playback of the bot segments.
         * This should normally be implemented by setting a boolean to false and checking that boolean at the start of your Update loop to allow execution when un-paused.</summary>
         */
        public void UnPauseAction();
    }
}
