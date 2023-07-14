using System.Net;
using System.Net.Sockets;
using JetBrains.Annotations;

namespace RegressionGames.Types
{
    public class RGClientConnection
    {
        public readonly uint clientId;
        [CanBeNull] public TcpClient client;
        public bool handshakeComplete = false;
        public string lifecycle;
        public bool connecting = false;
        [CanBeNull] public RGBotInstanceExternalConnectionInfo connectionInfo;

        public RGClientConnection(uint clientId, string lifecycle = "MANAGED", [CanBeNull] RGBotInstanceExternalConnectionInfo connectionInfo = null, [CanBeNull] TcpClient client = null)
        {
            this.clientId = clientId;
            this.lifecycle = lifecycle;
            this.client = client;
            this.connectionInfo = connectionInfo;
        }

        public bool Connected 
        {
            get
            {
                IPEndPoint ep = ((IPEndPoint)this.client?.Client.RemoteEndPoint);
                if (ep != null && ep.Port == connectionInfo.port && AddressesEqual(ep.Address.ToString(), connectionInfo.address))
                {
                    return this.client.Connected;
                }

                // not connected or port/address mis-match.. need to re-connect
                return false;
            }
        }

        private bool AddressesEqual(string address1, string address2)
        {
            // normalize localhost
            if (address1 == "127.0.0.1")
            {
                address1 = "localhost";
            }
            
            if (address2 == "127.0.0.1")
            {
                address2 = "localhost";
            }

            return address1.Equals(address2);
        }
    }
}
