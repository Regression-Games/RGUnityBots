namespace RGBotConfigs.RGStateProviders
{
    public interface RGStatePlatformer2DPlayerStatsProvider
    {
        /**
         * <summary> Get the current horizontal velocity of the player </summary>
         */
        public float Velocity();

        /**
         * <summary> Get the max horizontal velocity of the player </summary>
         */
        public float MaxVelocity();
        
        /**
         * <summary> Get the current height that the player can jump </summary>
         */
        public float JumpHeight();
        
        /**
         * <summary> Get the max height that the player can jump</summary>
         */
        public float MaxJumpHeight();
        
        /**
         * <summary> Get the current max safe fall height for the player.  From this height the player will not take damage.&lt;0 means they can fall any height without damage </summary>
         */
        public float SafeFallHeight();
        
        /**
         * <summary> Get the current max non fatal fall height for the player.  From this height the player will take damage, but will not die from the fall. &lt;0 means they can fall any height without damage </summary>
         */
        public float NonFatalFallHeight();

        /**
         * <summary>Is the player on the ground and not in a state of jumping </summary>
         */
        public bool IsOnGround();

        /**
         * <summary>Gravity unit to use for calculations</summary>
         */
        public float Gravity();
    }
}
