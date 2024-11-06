namespace StateRecorder.BotSegments.Models
{
    public interface IRGRestartGameAction
    {
        /**
         * <summary>Implement this interface to provide an implementation of how to safely cleanup and restart your game.  If this interface is not implemented when RestartGame Bot Action is used, then the default action will be to call SceneManager.LoadScene(sceneBuildIndex: 0, mode: LoadSceneMode.Single)</summary>
         */
        public void RestartGame();
    }
}
