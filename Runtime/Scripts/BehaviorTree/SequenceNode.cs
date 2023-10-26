using System.Linq;
using JetBrains.Annotations;
using RegressionGames;
using RegressionGames.RGBotLocalRuntime;
using TreeEditor;

namespace RegressionGames.BehaviorTree
{
    /// <summary>
    /// Represents a node that will run all of it's children in sequence.
    /// If any child returns <see cref="NodeStatus.Failure"/>, this node will fail and stop executing children.
    /// </summary>
    public class SequenceNode : ContainerNode
    {
        public override NodeStatus Execute(RG rgObject)
        {
            foreach(var child in Children)
            {
                var result = child.Execute(rgObject);
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