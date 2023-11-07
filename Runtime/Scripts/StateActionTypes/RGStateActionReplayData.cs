using JetBrains.Annotations;

namespace RegressionGames.StateActionTypes
{
    public class RGStateActionReplayData
    {
        [CanBeNull] public RGActionRequest[] actions;

        [CanBeNull] public RGValidationResult[] validationResults;

        [CanBeNull] public string error;

        [CanBeNull] public long? playerId;

        [CanBeNull] public long? sceneId;

        public RGTickInfoData tickInfo;

        /**
         * This is the server tick rate based on the physics timer
         * .. every 0.02 a physics timer happens, so 50 ticks would be 1.00 sec or 1000ms
         */
        [CanBeNull] public int? tickRate;

        public RGStateActionReplayData([CanBeNull] RGActionRequest[] actions, [CanBeNull] RGValidationResult[] validationResults, [CanBeNull] string error, long? playerId, long? sceneId, RGTickInfoData tickInfo, int? tickRate)
        {
            this.actions = actions;
            this.validationResults = validationResults;
            this.error = error;
            this.playerId = playerId;
            this.sceneId = sceneId;
            this.tickInfo = tickInfo;
            this.tickRate = tickRate;
        }
    }
}
