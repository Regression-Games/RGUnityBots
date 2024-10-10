namespace RegressionGames.RemoteOrchestration.Models
{
    // ReSharper disable InconsistentNaming
    public enum WorkAssignmentStatus
    {
        CONFLICT,
        WAITING_TO_START,
        IN_PROGRESS,
        CANCELLED,
        COMPLETE_SUCCESS,
        COMPLETE_TIMEOUT,
        COMPLETE_ERROR,
        OFFLINE // Not Used in the SDK Client, but here for completeness to match backend statuses
    }
    // ReSharper enable InconsistentNaming
}
