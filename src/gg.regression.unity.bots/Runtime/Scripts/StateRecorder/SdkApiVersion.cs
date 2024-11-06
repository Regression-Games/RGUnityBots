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
        /*
         * * <summary>added cvtext mouse click actions</summary>
         */
        public const int VERSION_14 = 14;
        /**
         * <summary>
         * rename 'keyFrameCriteria' to 'endCriteria' in json and allow empty/null criteria for segments
         * remove 'isPressed' from keyboardInputActionData
         * </summary>
         */
        public const int VERSION_15 = 15;
        /*
         * <summary> Added CV Object Detection Key Frame Criteria. </summary>
         */
        public const int VERSION_16 = 16;
        /*
         * <summary> Added CV Object Detection text mouse action. </summary>
         */
        public const int VERSION_17 = 17;
        /*
         * <summary> Added file:// and resource:// support to CVImage data. </summary>
         */
        public const int VERSION_18 = 18;
        /*
         * <summary> Added CV Object Detection threshold. </summary>
         */
        public const int VERSION_19 = 19;
        /**
         * <summary> Removed 'type' field from bot sequence entry</summary>
         */
        public const int VERSION_20 = 20;
        /**
         * <summary> Define first version of RGGameMetadata. Add fields to FrameStateData</summary>
         */
        public const int VERSION_21 = 21;
        /**
         * <summary> Removed addedCount and removedCount from path criteria</summary>
         */
        public const int VERSION_22 = 22;
        /**
         * <summary> More detailed camera clipping plane information in state</summary>
         */
        public const int VERSION_23 = 23;
        /**
         * <summary> View frustrum object inclusion changes</summary>
         */
        public const int VERSION_24 = 24;
        /**
         * <summary> Add restart game bot action</summary>
         */
        public const int VERSION_25 = 25;

        // Update this when new features are used in the SDK
        public const int CURRENT_VERSION = VERSION_25;
    }
}
