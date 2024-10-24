using System;
using JetBrains.Annotations;
using RegressionGames.RemoteOrchestration.Models;

namespace RegressionGames.RemoteOrchestration.Types
{
    [Serializable]
    public class SDKClientHeartbeatResponse
    {
        [CanBeNull]
        public WorkAssignment workAssignment;
    }
}
