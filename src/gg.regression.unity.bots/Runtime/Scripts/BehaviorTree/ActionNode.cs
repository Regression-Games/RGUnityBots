namespace RegressionGames.BehaviorTree
{
    /// <summary>
    /// Represents a node that runs custom user code to instruct the bot to perform an action.
    /// </summary>
    public abstract class ActionNode : BehaviorTreeNode
    {
        protected ActionNode(string name) : base(name)
        {
        }
    }
}