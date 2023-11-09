using System;
using Codice.CM.Common;
using RegressionGames.RGBotLocalRuntime;

namespace RegressionGames.BehaviorTree
{
    /// <summary>
    /// Base class for decorator nodes.
    /// </summary>
    public abstract class DecoratorNode : BehaviorTreeNode
    {
        protected readonly BehaviorTreeNode Child;

        /// <summary>
        /// Creates a new decorator node with the specified name and no child node.
        /// </summary>
        /// <param name="name">The name of the node.</param>
        protected DecoratorNode(string name) : base(name)
        {
        }

        /// <summary>
        /// Creates a new decorator node with the specified name and child node.
        /// </summary>
        /// <param name="name">The name of the node.</param>
        /// <param name="child">The <see cref="BehaviorTreeNode"/> child of this node.</param>
        protected DecoratorNode(string name, BehaviorTreeNode child) : base(name)
        {
            Child = child;
        }
    }

    /// <summary>
    /// A node that executes it's child, but ignores the result and always returns <see cref="NodeStatus.Success"/>.
    /// </summary>
    public class AlwaysSucceed: DecoratorNode
    {
        public AlwaysSucceed() : base("Always Succeed")
        {
        }

        public AlwaysSucceed(BehaviorTreeNode child) : base("Always Succeed", child)
        {
        }

        protected override NodeStatus Execute(RG rgObject)
        {
            _ = Child?.Invoke(rgObject);
            return NodeStatus.Success;
        }
    }

    /// <summary>
    /// A node that executes it's child, but ignores the result and always returns <see cref="NodeStatus.Failure"/>.
    /// </summary>
    public class AlwaysFail: DecoratorNode
    {
        public AlwaysFail() : base("Always Fail")
        {
        }

        public AlwaysFail(BehaviorTreeNode child) : base("Always Fail", child)
        {
        }

        protected override NodeStatus Execute(RG rgObject)
        {
            _ = Child?.Invoke(rgObject);
            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// A node that inverts the result of it's child node.
    /// </summary>
    /// <remarks>
    /// If the child node returns <see cref="NodeStatus.Running"/>, this node returns <see cref="NodeStatus.Running"/>.
    /// If the child node returns <see cref="NodeStatus.Failure"/>, this node returns <see cref="NodeStatus.Success"/>.
    /// If the child node returns <see cref="NodeStatus.Success"/>, this node returns <see cref="NodeStatus.Failure"/>.
    /// </remarks>
    public class Invert: DecoratorNode
    {
        public Invert() : base("Invert")
        {
        }

        public Invert(BehaviorTreeNode child) : base("Invert", child)
        {
        }

        protected override NodeStatus Execute(RG rgObject) =>
            Child?.Invoke(rgObject) switch
            {
                null => throw new InvalidOperationException("Invert node must have a child"),
                NodeStatus.Success => NodeStatus.Failure,
                NodeStatus.Failure => NodeStatus.Success,
                NodeStatus.Running => NodeStatus.Running,
                var other => throw new InvalidOperationException($"Unexpected node status: {other}"),
            };
    }
}
