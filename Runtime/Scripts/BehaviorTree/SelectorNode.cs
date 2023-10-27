using RegressionGames.RGBotLocalRuntime;

namespace RegressionGames.BehaviorTree
{
    /// <summary>
    /// Represents a node that will run each of its children in sequence.
    /// If any child returns <see cref="NodeStatus.Success"/> or <see cref="NodeStatus.Running"/>, this node will return that result and stop executing children.
    /// </summary>
    public class SelectorNode : ContainerNode
    {
        protected override NodeStatus Execute(RG rgObject)
        {
            foreach(var child in Children)
            {
                var result = child.Invoke(rgObject);
                if (result == NodeStatus.Success || result == NodeStatus.Running)
                {
                    return result;
                }
            }

            return NodeStatus.Success;
        }

        public SelectorNode(string name) : base(name)
        {
        }
    }
}