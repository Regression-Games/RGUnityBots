﻿namespace RegressionGames.StateRecorder
{
    public static class SdkApiVersion
    {
        // these values reference key moments in the development of the SDK for bot segments and state recording
        public const int VERSION_1 = 1; // initial bot segments version with and/or/normalizedPath criteria and mouse/keyboard input actions
        public const int VERSION_2 = 2; // added mouse pixel and object random clicking actions to bot segments
        public const int VERSION_3 = 3; // added ui pixel hash key frame criteria to bot segments
        public const int VERSION_4 = 4; // changed state format to enable entity support including 'type' and 'components' fields; added versioning to 'state' recording not just 'bot_segments'

        // Update this when new features are used in the SDK
        public const int CURRENT_VERSION = VERSION_4;
    }
}