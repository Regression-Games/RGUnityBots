using System;
using RegressionGames.RGBotLocalRuntime;

namespace RegressionGames.BehaviorTree
{
    /// <summary>
    /// A node that always returns <see cref="NodeStatus.Success"/> when executed.
    /// </summary>
    public class AlwaysSucceed: BehaviorTreeNode
    {
        public AlwaysSucceed() : base("Always Succeed")
        {
        }

        public override NodeStatus Execute(RG rgObject) => NodeStatus.Success;
    }
    
    /// <summary>
    /// A node that always returns <see cref="NodeStatus.Failure"/> when executed.
    /// </summary>
    public class AlwaysFail: BehaviorTreeNode
    {
        public AlwaysFail() : base("Always Fail")
        {
        }

        public override NodeStatus Execute(RG rgObject) => NodeStatus.Failure;
    }
    
    /// <summary>
    /// A node that inverts the result of it's child node.
    /// </summary>
    /// <remarks>
    /// If the child node returns <see cref="NodeStatus.Running"/>, this node returns <see cref="NodeStatus.Running"/>.
    /// If the child node returns <see cref="NodeStatus.Failure"/>, this node returns <see cref="NodeStatus.Success"/>.
    /// If the child node returns <see cref="NodeStatus.Success"/>, this node returns <see cref="NodeStatus.Failure"/>.
    /// </remarks>
    public class Invert: BehaviorTreeNode
    {
        private readonly BehaviorTreeNode _child;

        public Invert(BehaviorTreeNode child) : base("Invert")
        {
            _child = child;
        }

        public override NodeStatus Execute(RG rgObject) =>
            _child.Execute(rgObject) switch
            {
                NodeStatus.Success => NodeStatus.Failure,
                NodeStatus.Failure => NodeStatus.Success,
                NodeStatus.Running => NodeStatus.Running,
                var other => throw new InvalidOperationException($"Unexpected node status: {other}"),
            };
    }
}