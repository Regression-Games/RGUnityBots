using RegressionGames.RGBotLocalRuntime;

namespace RegressionGames.BehaviorTree
{
    /// <summary>
    /// Represents a node that will run all of its children in sequence.
    /// If any child returns <see cref="NodeStatus.Failure"/>, this node will fail and stop executing children.
    /// </summary>
    public class SequenceNode : ContainerNode
    {
        protected override NodeStatus Execute(RG rgObject)
        {
            foreach (var child in Children)
            {
                var result = child.Invoke(rgObject);
                if (result != NodeStatus.Success)
                {
                    return result;
                }
            }

            return NodeStatus.Success;
        }

        public SequenceNode(string name) : base(name)
        {
        }
    }
}