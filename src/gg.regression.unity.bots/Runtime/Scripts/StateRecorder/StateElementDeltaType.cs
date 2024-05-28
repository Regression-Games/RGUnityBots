namespace StateRecorder
{
    public enum StateElementDeltaType
    {
        /**
         * <summary>the current state count must be zero for this path</summary>
         */
        Zero,
        /**
         * <summary>count must decrease... this means the current state count must be <= keyframe count for this path</summary>
         */
        Decreased,
        /**
         * <summary>count must increase... this means the current state count must be >= keyframe count for this path</summary>
         */
        Increased,
        /**
         * <summary> makes sure the current state count for this path is non-zero</summary>
         */
        NonZero
    }
}
