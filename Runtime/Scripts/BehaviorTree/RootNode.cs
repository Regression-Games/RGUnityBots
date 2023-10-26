using System.Collections.Generic;
using System.Linq;
using RegressionGames.RGBotLocalRuntime;

namespace RegressionGames.BehaviorTree
{
    /// <summary>
    /// Represents the root node of a behavior tree.
    /// The root node carries the data dictionary that is shared between all nodes in the tree.
    /// </summary>
    public class RootNode : ContainerNode
    {
        internal readonly Dictionary<string, object> Data = new();

        public RootNode() : base("Root Node")
        {
        }

        public override NodeStatus Execute(RG rgObject)
        {
            return Children.Count > 0 
                ? Children.First().Execute(rgObject) 
                : NodeStatus.Failure;
        }
    }
}