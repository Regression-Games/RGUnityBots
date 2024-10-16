using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.RemoteOrchestration.Types
{
    [Serializable]
    public class AvailableBotSequence
    {
        public string name;
        public string description;
        public string resourcePath;

        public AvailableBotSequence(string resourcePath, BotSequence sequence)
        {
            this.resourcePath = resourcePath;
            this.name = sequence.name;
            this.description = sequence.description;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, name);
            stringBuilder.Append(",\"description\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, description);
            stringBuilder.Append(",\"resourcePath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, resourcePath);
            stringBuilder.Append("}");
        }
    }

    [Serializable]
    public class SDKClientRegistrationRequest
    {

        private readonly Guid clientGuid;

        public SDKClientRegistrationRequest(Guid id)
        {
            this.clientGuid = id;
        }

        public List<AvailableBotSequence> availableSequences;

        private static ThreadLocal<StringBuilder> _stringBuilder = new ThreadLocal<StringBuilder>(() => new(1000));

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, SdkApiVersion.CURRENT_VERSION);
            // clientGuid is not currently used by backend, but is helpful for debugging initial registration
            stringBuilder.Append(",\"clientGuid\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, clientGuid.ToString());
            stringBuilder.Append(",\"utcTime\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            stringBuilder.Append(",\"availableSequences\":[");
            var availableSequencesCount = availableSequences.Count;
            for (var i = 0; i < availableSequencesCount; i++)
            {
                availableSequences[i].WriteToStringBuilder(stringBuilder);
                if (i + 1 < availableSequencesCount)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("],\"metadata\":");
            WriteMetadataToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }

        private void WriteMetadataToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"ipAddresses\":[");
            var ipAddresses = GetLocalIPSv4();
            for (var i = ipAddresses.Length; i >= 0; i--)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, ipAddresses[i]);
                if (i > 0)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("],\"dataPath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, Application.dataPath);
            stringBuilder.Append(",\"pid\":");
            IntJsonConverter.WriteToStringBuilderNullable(stringBuilder, GetProcessId());
            stringBuilder.Append(",\"platform\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, Application.platform.ToString());
            // NOTE: Hopefully this is enough to identify which specific instance this is...
            // For example, if I run N instances of the game on a single machine in parallel, is this information enough to know which one this is.
            stringBuilder.Append("}");
        }

        public string ToJsonString()
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder( _stringBuilder.Value);
            return  _stringBuilder.Value.ToString();
        }

        // cache this to avoid repeated resolution on each heartbeat
        private static int? _processId = null;
        private static int? GetProcessId()
        {
            if (_processId == null)
            {
                try
                {
                    using var thisProcess = System.Diagnostics.Process.GetCurrentProcess();
                    _processId = thisProcess.Id;
                }
                catch (Exception)
                {
                    // do nothing
                }
            }
            return _processId;

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
