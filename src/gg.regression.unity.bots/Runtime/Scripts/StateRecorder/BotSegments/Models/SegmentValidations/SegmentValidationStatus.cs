namespace StateRecorder.BotSegments.Models.SegmentValidations
{
    public enum SegmentValidationStatus
    {
        /**
         * This validation is currently passing.
         */
        PASSED,
        
        /**
         * This validation is current failing.
         */
        FAILED,
        
        /**
         * This validation has not yet been set, it is unknown if it will pass or fail yet.
         */
        UNKNOWN
    }
}