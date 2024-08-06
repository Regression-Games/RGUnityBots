namespace RegressionGames.StateRecorder
{
    public static class SdkApiVersion
    {
        // these values reference key moments in the development of the SDK for bot segments and state recording
        public const int VERSION_1 = 1; // initial bot segments version with and/or/normalizedPath criteria and mouse/keyboard input actions
        public const int VERSION_2 = 2; // added mouse pixel and object random clicking actions to bot segments
        public const int VERSION_3 = 3; // added ui pixel hash key frame criteria to bot segments
        public const int VERSION_4 = 4; // changed state format to enable entity support including 'type' and 'components' fields; added versioning to 'state' recording not just 'bot_segments'
        public const int VERSION_5 = 5; // added support for monobehaviour bot segment actions
        public const int VERSION_6 = 6; // add support for monkey bot
        public const int VERSION_7 = 7; // added partial normalized path matching
        public const int VERSION_8 = 8; // add cv text key frame criteria matching
        public const int VERSION_9 = 9; // add cv image key frame criteria matching
        public const int VERSION_10 = 10; // add criteria for completion of bot actions

        // Update this when new features are used in the SDK
        public const int CURRENT_VERSION = VERSION_10;
    }
}
