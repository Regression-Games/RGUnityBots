using RegressionGames.RGBotLocalRuntime;

namespace RegressionGames.BehaviorTree
{
    public abstract class RGBehaviorTreeBot: RGUserBot
    {
        private BehaviorTreeNode _rootNode;

        internal override void Init(long botId, string botName)
        {
            base.Init(botId, botName);
            _rootNode = BuildBehaviorTree();
        }

        public override void ProcessTick(RG rgObject)
        {
            var result = _rootNode.Execute(rgObject);
            RGDebug.LogVerbose($"Behavior tree completed with status: {result}");
        }

        protected abstract RootNode BuildBehaviorTree();
    }
}