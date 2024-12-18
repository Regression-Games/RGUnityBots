namespace StateRecorder.BotSegments.Models.SegmentValidations
{
    /**
     * <summary>
     * The mode in which to apply the validaton
     * </summary>
     */
    public enum ValidationMode
    {
        /**
         * This condition should be true on every frame that it is
         * evaluated.
         */
        ALWAYS_TRUE,
        
        /**
         * This condition should never be true for any frame it is
         * evaluated.
         */
        NEVER_TRUE,
        
        /**
         * This condition should be true at least once during the
         * test.
         */
        EVENTUALLY_TRUE,
        
        /**
         * Once there is a frame that the given condition turns true, it must always
         * be marked as true.
         */
        ONCE_TRUE_ALWAYS_TRUE
    }
}