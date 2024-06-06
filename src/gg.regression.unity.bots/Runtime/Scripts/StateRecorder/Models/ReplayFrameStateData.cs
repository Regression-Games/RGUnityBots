using System;
using System.Diagnostics.CodeAnalysis;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    // replay doesn't need to deserialize everything we record
    public class ReplayFrameStateData :BaseFrameStateData
    {
        /**
         * <summary>UUID of the session</summary>
         */
        public long recordingSessionId;
    }
}
