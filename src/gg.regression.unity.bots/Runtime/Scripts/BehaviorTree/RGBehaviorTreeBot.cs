using UnityEngine;

namespace RegressionGames.BehaviorTree
{
    public abstract class RGBehaviorTreeBot : MonoBehaviour, IRGBot
    {
        private BehaviorTreeNode _rootNode;

        public void Start()
        {
            _rootNode = BuildBehaviorTree();
        }

        public void Update()
        {
            _ = _rootNode.Invoke(this.gameObject);
        }

        protected abstract RootNode BuildBehaviorTree();
    }
}
