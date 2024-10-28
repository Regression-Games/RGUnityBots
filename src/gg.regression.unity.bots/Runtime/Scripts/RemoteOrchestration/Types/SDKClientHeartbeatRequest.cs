using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using RegressionGames.RemoteOrchestration.Models;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.JsonConverters;

// ReSharper disable InconsistentNaming
namespace RegressionGames.RemoteOrchestration.Types
{
    [Serializable]
    public class SDKClientHeartbeatRequest : IStringBuilderWriteable
    {

        public long clientId;
        public ActiveSequence activeSequence;
        public WorkAssignment activeWorkAssignment;
        public Guid clientGuid;

        private static ThreadLocal<StringBuilder> _stringBuilder = new ThreadLocal<StringBuilder>(() => new(1000));

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"clientId\":");
            LongJsonConverter.WriteToStringBuilder(stringBuilder, clientId);
            // clientGuid is not currently used by backend, but is helpful for debugging initial registration
            stringBuilder.Append(",\"clientGuid\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, clientGuid.ToString());
            stringBuilder.Append(",\"utcTime\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            stringBuilder.Append(",\"activeSequence\":");
            if (activeSequence == null)
            {
                stringBuilder.Append("null");
            }
            else
            {
                activeSequence.WriteToStringBuilder(stringBuilder);
            }
            stringBuilder.Append(",\"activeWorkAssignment\":");
            if (activeWorkAssignment == null)
            {
                stringBuilder.Append("null");
            }
            else
            {
                activeWorkAssignment.WriteToStringBuilder(stringBuilder);
            }
            stringBuilder.Append("}");
        }

        private void WriteMetadataToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"ipAddresses\":[");
            var ipAddresses = GetLocalIPSv4();
            for (var i = ipAddresses.Length-1; i >= 0; i--)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, ipAddresses[i]);
                if (i > 0)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("]}");
        }

        public string ToJsonString()
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder( _stringBuilder.Value);
            return  _stringBuilder.Value.ToString();
        }

        // cache this to avoid repeated dns resolution on each heartbeat
        private static string[] _ipAddresses;

        private static string[] GetLocalIPSv4()
        {
            if (_ipAddresses == null)
            {
                var addressList = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
                _ipAddresses = addressList.Where(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(f=> f.ToString()).ToArray();
            }
            return _ipAddresses;
        }
    }
}
