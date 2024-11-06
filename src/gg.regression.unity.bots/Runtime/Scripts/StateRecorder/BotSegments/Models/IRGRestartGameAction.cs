namespace StateRecorder.BotSegments.Models
{
    public interface IRGRestartGameAction
    {
        /**
         * <summary>Implement this interface to provide an implementation of how to safely cleanup and restart your game.  If this interface is not implemented when RestartGame Bot Action is used in a build of your game outside the UnityEditor, then the default action will be to call SceneManager.LoadScene(sceneBuildIndex: 0, mode: LoadSceneMode.Single).  In the UnityEditor, the action will always be to stop and re-enter play mode; this interface will not be used.</summary>
         */
        public void RestartGame();
    }
}
