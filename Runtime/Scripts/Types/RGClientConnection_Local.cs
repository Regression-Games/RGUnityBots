
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.StateActionTypes;

namespace RegressionGames.Types
{
    public class RGClientConnection_Local : RGClientConnection
    {

        private RGBotRunner _runner = null;
        
        public RGClientConnection_Local(uint clientId, string lifecycle = "MANAGED") : base(clientId, RGClientConnectionType.LOCAL, lifecycle)
        {
        }

        public void SetBotRunner(RGBotRunner runner)
        {
            this._runner = runner;
        }

        public override void Connect()
        {
            RGBotServerListener.GetInstance()?.SetUnityBotState(ClientId, RGUnityBotState.CONNECTING);
        }

        public override bool SendTeardown()
        {
            if (Connected())
            {
                _runner.TeardownBot();
                _runner = null;
                return true;
            }

            return false;
        }

        public override bool SendTickInfo(RGTickInfoData tickInfo)
        {
            if (Connected())
            {
                _runner.QueueTickInfo(tickInfo);
                return true;
            }

            return false;
        }

        public override bool SendHandshakeResponse(RGServerHandshake handshake)
        {
            if (Connected())
            {
                _runner.ProcessServerHandshake(handshake);
                return true;
            }

            return false;
        }

        public override bool Connected()
        {
            return this._runner != null;
        }
        
    }
    
}
