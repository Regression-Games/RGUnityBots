using System;

namespace RegressionGames.Types
{
    public class RGUnityBotState
    {
        private RGUnityBotState(string value) { _value = value; }

        private readonly string _value;
        
        public static RGUnityBotState STARTING   { get { return new RGUnityBotState("STARTING"); } }
        public static RGUnityBotState CONNECTING   { get { return new RGUnityBotState("CONNECTING"); } }
        public static RGUnityBotState CONNECTED    { get { return new RGUnityBotState("CONNECTED"); } }
        public static RGUnityBotState RUNNING    { get { return new RGUnityBotState("RUNNING"); } }
        public static RGUnityBotState TEARING_DOWN    { get { return new RGUnityBotState("TEARING_DOWN"); } }
        public static RGUnityBotState UNKNOWN { get { return new RGUnityBotState("UNKNOWN"); } }

        public override string ToString()
        {
            return _value;
        }

        public override bool Equals(Object state)
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