using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using RegressionGames.RemoteOrchestration.Models;
using RegressionGames.RemoteOrchestration.Types;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.ClientDashboard
{
    public enum TcpMessageType
    {
        // =====================
        // client -> server
        // =====================
        
        Ping,
        
        // client requests to play a resource with the given resourcePath
        PlaySequence,
        PlaySegment,
        
        // stops any currently-running sequence/segments
        StopReplay,
        
        // =====================
        // server -> client
        // =====================
        
        Pong,
        
        // info about the available file-based resources for this game instance
        AvailableSequences,
        AvailableSegments,
        
        // info about the currently-running sequence (or segment)
        ActiveSequence,
        
        // sent prior to closing Unity windows.
        // this tells any running client windows to also close.
        CloseConnection
    }

    public interface ITcpMessageData : IStringBuilderWriteable { }
    
    /// <summary>
    /// Format for messages sent between client and server in either direction
    /// </summary>
    [Serializable]
    public class TcpMessage : IStringBuilderWriteable
    {
        public TcpMessageType type;
        [CanBeNull] public ITcpMessageData payload; // JSON string
        
        public override string ToString()
        {
            return ((IStringBuilderWriteable) this).ToJsonString();
        }
        
        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, type.ToString());

            if (payload != null)
            {
                stringBuilder.Append(",\"payload\":");
                payload.WriteToStringBuilder(stringBuilder);
            }
           
            stringBuilder.Append("}");
        }
    }
    
    [Serializable]
    public class ActiveSequenceTcpMessageData : ITcpMessageData
    {
        [CanBeNull] public ActiveSequence activeSequence;
        
        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"activeSequence\":");
            if (activeSequence == null)
            {
                stringBuilder.Append("null");
            }
            else
            {
                activeSequence.WriteToStringBuilder(stringBuilder);
            }
            stringBuilder.Append("}");
        }
        
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(1000);
            WriteToStringBuilder(sb);
            return sb.ToString();
        }
    }
    
    [Serializable]
    public class AvailableSequencesTcpMessageData : ITcpMessageData
    {
        public List<AvailableBotSequence> availableSequences;
        
        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"availableSequences\":[");
            var availableSequencesCount = availableSequences.Count;
            for (var i = 0; i < availableSequencesCount; i++)
            {
                availableSequences[i].WriteToStringBuilder(stringBuilder);
                if (i + 1 < availableSequencesCount)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("]}");
        }
        
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(1000);
            WriteToStringBuilder(sb);
            return sb.ToString();
        }
    }
    
    [Serializable]
    public class AvailableSegmentsTcpMessageData : ITcpMessageData
    {
        public List<BotSequenceEntry> availableSegments;
        
        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"availableSegments\":[");
            var availableSegmentsCount = availableSegments.Count;
            for (var i = 0; i < availableSegmentsCount; i++)
            {
                // a lot of the fields on BotSequenceEntry are not written to string builder
                // so pick what we need for the dashboard here...
                var currentSegment = availableSegments[i];
                
                stringBuilder.Append("{\"apiVersion\":");
                IntJsonConverter.WriteToStringBuilder(stringBuilder, currentSegment.apiVersion);
                
                // not normally serialized
                stringBuilder.Append(",\"resourcePath\":");
                StringJsonConverter.WriteToStringBuilder(stringBuilder, currentSegment.resourcePath);
                stringBuilder.Append(",\"type\":");
                StringJsonConverter.WriteToStringBuilder(stringBuilder, currentSegment.type.ToString());
                stringBuilder.Append(",\"name\":");
                StringJsonConverter.WriteToStringBuilder(stringBuilder, currentSegment.name);
                stringBuilder.Append(",\"description\":");
                StringJsonConverter.WriteToStringBuilder(stringBuilder, currentSegment.description);
                
                stringBuilder.Append("}");
                
                if (i + 1 < availableSegmentsCount)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("]}");
        }
        
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(1000);
            WriteToStringBuilder(sb);
            return sb.ToString();
        }
    }
    
    [Serializable]
    public class PlayResourceTcpMessageData : ITcpMessageData
    {
        public string resourcePath;
        
        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"resourcePath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, resourcePath);
            stringBuilder.Append("}");
        }
        
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(1000);
            WriteToStringBuilder(sb);
            return sb.ToString();
        }
    }
    
}