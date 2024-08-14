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
         * <summary>added support for monkey bot</summary>
         */
        public const int VERSION_6 = 6;
        /**
         * <summary>added partial normalized path matching</summary>
         */
        public const int VERSION_7 = 7;
        /**
         * <summary>added cv text key frame criteria matching</summary>
         */
        public const int VERSION_8 = 8;
        /**
         * <summary>added cv image key frame criteria matching, redefine withinRect data format</summary>
         */
        public const int VERSION_9 = 9;
        /**
         * <summary>added cv image mouse actions</summary>
         */
        public const int VERSION_10 = 10;
        /**
         * <summary>added criteria to wait for completion of bot actions</summary>
         */
        public const int VERSION_11 = 11;
        /**
         * <summary>
         * introduce ability to configure bot segments into sequences
         * remove 'action' from keyboardInputActionData (just use binding)
         * </summary>
         */
        public const int VERSION_12 = 12;
        /**
         * <summary>add code coverage recording support</summary>
         */
        public const int VERSION_13 = 13;

        /**
         * <summary>
         * rename 'keyFrameCriteria' to 'endCriteria' in json and allow empty/null criteria for segments
         * remove 'isPressed' from keyboardInputActionData
         * </summary>
         */
        public const int VERSION_15 = 15;


        // Update this when new features are used in the SDK
        public const int CURRENT_VERSION = VERSION_15;
    }
}
