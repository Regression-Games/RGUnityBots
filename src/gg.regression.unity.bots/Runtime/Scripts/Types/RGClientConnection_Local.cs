
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RegressionGames.Types
{
    public class RGClientConnection_Local : RGClientConnection
    {

        private RGBotRunner _runner = null;
        private RGBotDelegate _botDelegate = null;

        public RGClientConnection_Local(long clientId, RGBotDelegate botDelegate, string lifecycle = "MANAGED") : base(clientId, RGClientConnectionType.LOCAL, lifecycle)
        {
            _botDelegate = botDelegate;
            _botDelegate.clientId = clientId;
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
            if (_botDelegate != null)
            {
                Object.Destroy(_botDelegate);
                _botDelegate = null;
            }

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
