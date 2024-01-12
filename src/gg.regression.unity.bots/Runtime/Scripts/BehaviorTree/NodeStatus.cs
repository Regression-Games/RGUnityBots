namespace RegressionGames.BehaviorTree
{
    public enum NodeStatus
    {
        /// <summary>
        /// The node has completed successfully.
        /// </summary>
        Success,

        /// <summary>
        /// The node failed to execute.
        /// </summary>
        Failure,

        /// <summary>
        /// The node is still running.
        /// </summary>
        Running,
    }
}