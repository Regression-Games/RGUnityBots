using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGBotInstanceExternalConnectionInfo
    {
        public long botInstanceId;
        public string address;
        public int port;

        public override string ToString()
        {
            return $"{botInstanceId} - {address}:{port}";
        }
    }
}