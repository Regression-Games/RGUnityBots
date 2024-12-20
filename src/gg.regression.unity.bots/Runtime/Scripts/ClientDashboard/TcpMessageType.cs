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
        
        // request JSON contents for a file-based resource
        RequestSequenceJson,
        RequestSegmentJson,
        
        SaveSegment,
        DeleteSegment,
        
        // =====================
        // server -> client
        // =====================
        
        Pong,
        
        // info about the available file-based resources for this game instance
        AvailableSequences,
        AvailableSegments,
        
        // info about the currently-running sequence (or segment)
        ActiveSequence,
        
        // send JSON contents for a file-based resource
        SendSequenceJson,
        SendSegmentJson,
        
        // sent prior to closing Unity windows.
        // this tells any running client windows to also close.
        CloseConnection
    }
}