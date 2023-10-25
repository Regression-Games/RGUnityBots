using System;
using RegressionGames.StateActionTypes;

namespace RegressionGames.DataCollection
{
    
    [Serializable]
    public class ReplayDataPoint
    {
        
        public RGTickInfoData tickInfo;
        public long playerId;
        public RGActionRequest[] actions;
        public RGValidationResult[] validationResults;

        public ReplayDataPoint(RGTickInfoData tickInfo, long playerId, RGActionRequest[] actions, RGValidationResult[] validationResults)
        {
            this.tickInfo = tickInfo;
            this.playerId = playerId;
            this.actions = actions;
            this.validationResults = validationResults;
        }
    }
}