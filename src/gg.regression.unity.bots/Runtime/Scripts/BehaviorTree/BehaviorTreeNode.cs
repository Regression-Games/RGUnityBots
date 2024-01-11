using System;
using System.Collections.Generic;
using RegressionGames.RGBotLocalRuntime;

namespace RegressionGames.BehaviorTree
{
    /// <summary>
    /// Base class to represent a nodes in a Behavior Tree.
    /// </summary>
    public abstract class BehaviorTreeNode
    {
        /// <summary>
        /// A reference to the tree this node is a part of.
        /// </summary>
        protected BehaviorTreeNode Parent { get; private set; }

        /// <summary>The name of the node.</summary>
        public readonly string Name;

        /// <summary>The path from the root to this node.</summary>
        public string Path => Parent is null ? Name : $"{Parent.Path}/{Name}";

        /// <summary>
        /// Initializes the node with a name.
        /// </summary>
        /// <param name="name">The name of the node</param>
        protected BehaviorTreeNode(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Executes the node, given the provided <see cref="RG"/> object as context.
        /// </summary>
        /// <param name="rgObject">The <see cref="RG"/> context object.</param>
        /// <returns>A <see cref="NodeStatus"/> indicating the result of executing the node.</returns>
        public NodeStatus Invoke(RG rgObject)
        {
            // We use a public wrapper to call the protected Execute method so that we can log the result.
            var result = Execute(rgObject);
            RGDebug.LogVerbose($"Behavior Tree Node '{Path}' completed with result '{result}'");
            return result;
        }

        /// <summary>
        /// Executes the node, given the provided <see cref="RG"/> object as context.
        /// </summary>
        /// <param name="rgObject">The <see cref="RG"/> context object.</param>
        /// <returns>A <see cref="NodeStatus"/> indicating the result of executing the node.</returns>
        protected abstract NodeStatus Execute(RG rgObject);

        /// <summary>
        /// Sets a data value with the specified key and value.
        /// This value will be available to all nodes in the tree via <see cref="GetData{T}"/>.
        /// </summary>
        /// <param name="key">The key to store the data under.</param>
        /// <param name="value">The value to store.</param>
        protected void SetData(string key, object value)
            => GetRoot().Data[key] = value;

        /// <summary>
        /// Gets a data value with the specified key and expected type.
        /// </summary>
        /// <param name="key">The key to retrieve data from.</param>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <returns>The value at the provided key, cast to <typeparamref name="T"/>, or the default value for <typeparamref name="T"/> if no value is stored at that key.</returns>
        protected T GetData<T>(string key)
            => GetRoot().Data.TryGetValue(key, out var v) ? (T)v : default;

        /// <summary>
        /// Clears the data value at the specified key.
        /// </summary>
        /// <param name="key"></param>
        protected void ClearData(string key)
            => GetRoot().Data.Remove(key);

        internal void SetParent(BehaviorTreeNode parent)
            => Parent = parent;

        protected RootNode GetRoot()
        {
            // Walk up 'parent' links until we hit a null parent or a RootNode.
            var current = this;
            while (current is not null and not RootNode)
            {
                current = current.Parent;
            }

            if (current is not RootNode root)
            {
                // We broke from the loop without finding a root node
                throw new InvalidOperationException("Could not find root node.");
            }

            return root;
        }
    }

    /// <summary>
    /// Base class for any <see cref="BehaviorTreeNode"/> with child nodes.
    /// </summary>
    public abstract class ContainerNode : BehaviorTreeNode
    {
        private List<BehaviorTreeNode> _children = new();

        /// <summary>
        /// The children of this node.
        /// </summary>
        public IReadOnlyList<BehaviorTreeNode> Children => _children;

        /// <summary>
        /// Adds a child to this node.
        /// </summary>
        /// <param name="node">The <see cref="BehaviorTreeNode"/> to add as a child.</param>
        public void AddChild(BehaviorTreeNode node)
        {
            _children.Add(node);
            node.SetParent(this);
        }

        protected ContainerNode(string name) : base(name)
        {
        }
    }
}