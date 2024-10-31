using System;

namespace RegressionGames.Types
{

    [Serializable]
    public class PlaybackResult
    {

        /**
         * <summary>The location that the playback recording is saved to on disk.</summary>
         */
        public string saveLocation;

        /**
         * <summary>Did this playback succeed (true) or timeout (false)</summary>
         */
        public bool success;

        /**
         * <summary>The last status of the playback when it timed out.  Generally only populated when 'success' is false</summary>
         */
        public string statusMessage;

    }

}
