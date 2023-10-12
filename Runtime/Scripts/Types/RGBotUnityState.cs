namespace RegressionGames.Types
{
    public class RGUnityBotState
    {
        private readonly string _value;

        private RGUnityBotState(string value)
        {
            _value = value;
        }

        public static RGUnityBotState STARTING => new("STARTING");
        public static RGUnityBotState CONNECTING => new("CONNECTING");
        public static RGUnityBotState CONNECTED => new("CONNECTED");
        public static RGUnityBotState RUNNING => new("RUNNING");
        public static RGUnityBotState TEARING_DOWN => new("TEARING_DOWN");
        public static RGUnityBotState STOPPED => new("STOPPED");
        public static RGUnityBotState UNKNOWN => new("UNKNOWN");

        public override string ToString()
        {
            return _value;
        }

        public override bool Equals(object state)
        {
            var botState = state as RGUnityBotState;
            if (botState == null)
            {
                return false;
            }

            return _value.Equals(botState._value);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }
    }
}
