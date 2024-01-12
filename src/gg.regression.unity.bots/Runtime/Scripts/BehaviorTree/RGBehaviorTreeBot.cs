using RegressionGames.RGBotLocalRuntime;

namespace RegressionGames.BehaviorTree
{
    public abstract class RGBehaviorTreeBot : RGUserBot
    {
        private BehaviorTreeNode _rootNode;

        internal override void Init(long botId, string botName)
        {
            base.Init(botId, botName);
            _rootNode = BuildBehaviorTree();
        }

        public override void ProcessTick(RG rgObject)
        {
            _ = _rootNode.Invoke(rgObject);
        }

        protected abstract RootNode BuildBehaviorTree();
    }
}