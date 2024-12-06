using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using RegressionGames.RemoteOrchestration.Models;
using RegressionGames.RemoteOrchestration.Types;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames
{
    public enum TcpMessageType
    {
        // =====================
        // client -> server
        // =====================
        
        PING,
        
        // client requests to play a sequence with the given resourcePath
        PLAY_SEQUENCE,
        
        // =====================
        // server -> client
        // =====================
        
        PONG,
        
        // info about the available sequences for this game instance
        AVAILABLE_SEQUENCES,
        
        // info about the currently-running sequence (or segment)
        ACTIVE_SEQUENCE,
        
        // sent prior to closing Unity windows.
        // this tells any running client windows to also close.
        APPLICATION_QUIT
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
    public class PlaySequenceTcpMessageData : ITcpMessageData
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