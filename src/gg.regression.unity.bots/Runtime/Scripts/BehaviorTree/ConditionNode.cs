namespace RegressionGames.BehaviorTree
{
    /// <summary>
    /// Represents a node that runs custom user code to determine if a condition is met.
    /// </summary>
    public abstract class ConditionNode : BehaviorTreeNode
    {
        protected ConditionNode(string name) : base(name)
        {
        }
    }
}