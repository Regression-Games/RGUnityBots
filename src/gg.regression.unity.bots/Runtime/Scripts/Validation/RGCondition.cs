namespace RegressionGames.Validation
{
    
    /**
     * The condition that the test should be evaluated as.
     */
    public enum RGCondition
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
        EVENTUALLY_TRUE
    }
}