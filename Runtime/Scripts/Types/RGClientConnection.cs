using System.Net.Sockets;

namespace RegressionGames.Types
{
    public class RGClientConnection
    {
        public readonly TcpClient client;
        public readonly string lifecycle;

        public RGClientConnection(string lifecycle, TcpClient client)
        {
            this.lifecycle = lifecycle;
            this.client = client;
        }
    }
}
