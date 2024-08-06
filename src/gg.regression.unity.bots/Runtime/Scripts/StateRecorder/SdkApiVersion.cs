namespace RegressionGames.StateRecorder
{
    public static class SdkApiVersion
    {
        // these values reference key moments in the development of the SDK for bot segments and state recording
        /**
         * <summary>initial bot segments version with and/or/normalizedPath criteria and mouse/keyboard input actions</summary>
         */
        public const int VERSION_1 = 1;
        /**
         * <summary>added mouse pixel and object random clicking actions to bot segments</summary>
         */
        public const int VERSION_2 = 2;
        /**
         * <summary>added ui pixel hash key frame criteria to bot segments</summary>
         */
        public const int VERSION_3 = 3;
        /**
         * <summary>changed state format to enable entity support including 'type' and 'components' fields; added versioning to 'state' recording not just 'bot_segments'</summary>
         */
        public const int VERSION_4 = 4;
        /**
         * <summary>added support for monobehaviour bot segment actions</summary>
         */
        public const int VERSION_5 = 5;
        /**
         * <summary>add support for monkey bot</summary>
         */
        public const int VERSION_6 = 6;
        /**
         * <summary>added partial normalized path matching</summary>
         */
        public const int VERSION_7 = 7;
        /**
         * <summary>add cv text key frame criteria matching</summary>
         */
        public const int VERSION_8 = 8;
        /**
         * <summary>add cv image key frame criteria matching, redefine withinRect data format</summary>
         */
        public const int VERSION_9 = 9;
        /**
         * <summary>add cv image mouse actions</summary>
         */
        public const int VERSION_10 = 10;

        // Update this when new features are used in the SDK
        public const int CURRENT_VERSION = VERSION_10;
    }
}
