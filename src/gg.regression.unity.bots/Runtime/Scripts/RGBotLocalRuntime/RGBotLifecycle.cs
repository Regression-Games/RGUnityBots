namespace RegressionGames.RGBotLocalRuntime
{
    public class RGBotLifecycle
    {
        private string _type;
        
        private RGBotLifecycle(string type)
        {
            this._type = type;
        }
        
        public static readonly RGBotLifecycle MANAGED = new RGBotLifecycle("MANAGED");
        public static readonly RGBotLifecycle PERSISTENT = new RGBotLifecycle("PERSISTENT");

        public override string ToString()
        {
            return _type;
        }
    }
}