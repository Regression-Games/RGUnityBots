namespace StateRecorder.BotSegments.Models
{
    public interface IRGResetForNextTestAction
    {
        /**
         * <summary>Implement this interface to provide an implementation of how to safely reset states in your game to prepare for the next test.  If this interface is not implemented when ResetForNextTest Bot Action is used, then the default action will be to restart the game using RestartGame action.</summary>
         */
        public void ResetForNextTest();
    }
}
