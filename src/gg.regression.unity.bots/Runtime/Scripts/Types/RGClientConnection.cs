using RegressionGames.StateActionTypes;

namespace RegressionGames.Types
{
    public abstract class RGClientConnection
    {
        public readonly long ClientId;
        public string Lifecycle;

        public string Token;

        public readonly RGClientConnectionType Type;

        public RGClientConnection(long clientId, RGClientConnectionType type, string lifecycle = "MANAGED")
        {
            this.ClientId = clientId;
            this.Lifecycle = lifecycle;
            this.Type = type;
        }

        public virtual async void Connect()
        {

        }

        public abstract bool SendTeardown();

        public abstract bool SendTickInfo(RGTickInfoData tickInfo);

        public abstract bool SendHandshakeResponse(RGServerHandshake handshake);

        public abstract bool Connected();

        public virtual void Close()
        {
        }

    }

    public enum RGClientConnectionType
    {
        LOCAL,
        REMOTE
    }
}
