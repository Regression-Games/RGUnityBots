using System.Collections.Generic;
using System.Linq;
using RegressionGames.RGBotLocalRuntime;
using UnityEngine;

namespace RegressionGames.BehaviorTree
{
    /// <summary>
    /// Represents the root node of a behavior tree.
    /// The root node carries a data dictionary that is shared between all nodes in the tree.
    /// </summary>
    public class RootNode : ContainerNode
    {
        internal readonly Dictionary<string, object> Data = new();

        public RootNode() : base("Root Node")
        {
        }

        protected override NodeStatus Execute(GameObject rgObject)
        {
            return Children.Count > 0
                ? Children.First().Invoke(rgObject)
                : NodeStatus.Failure;
        }
    }
}
