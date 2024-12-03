using System;
using System.Text;
using JetBrains.Annotations;
using RegressionGames.RemoteOrchestration.Models;
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
        
        
        // =====================
        // server -> client
        // =====================
        
        PONG,
        
        // info about the currently-running sequence (or segment)
        ACTIVE_SEQUENCE,
        
        // sent prior to closing Unity windows.
        // this tells any running client windows to also close.
        APPLICATION_QUIT,
        
        
        // =====================
        // both directions
        // =====================
        
        // from server: forcefully close the client connection due to an exception. client can reconnect.
        // from client: gracefully close the connection on page refresh or closing the tab. this prevents errors on the Unity side.
        CLOSE
        
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

    // [Serializable]
    // public class EmptyTcpMessageData : ITcpMessageData
    // {
    //     public void WriteToStringBuilder(StringBuilder stringBuilder)
    //     {
    //         stringBuilder.Append("{}");
    //     }
    //     
    //     public override string ToString()
    //     {
    //         StringBuilder sb = new StringBuilder(1000);
    //         WriteToStringBuilder(sb);
    //         return sb.ToString();
    //     }
    // }
    
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
}