
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RegressionGames.Types
{
    public class RGClientConnection_Local : RGClientConnection
    {

        private RGBotRunner _runner = null;
        private RGBotController botController = null;
        private readonly bool hasBotController;

        public RGClientConnection_Local(long clientId, RGBotController botController, string lifecycle = "MANAGED") : base(clientId, RGClientConnectionType.LOCAL, lifecycle)
        {
            if (botController != null)
            {
                this.hasBotController = true;
                this.botController = botController;
                this.botController.clientId = clientId;
            }
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
            if (hasBotController)
            {
                Object.Destroy(botController);
                botController = null;

                // Nothing else to tear down.

                return true;
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
            if (hasBotController)
            {
                // The controller will handle ticking
                return false;
            }

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
            return hasBotController || _runner != null;
        }

    }

}
