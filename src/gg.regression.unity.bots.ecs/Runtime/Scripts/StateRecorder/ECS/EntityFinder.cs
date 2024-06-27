using System;
using System.Collections.Generic;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder.ECS
{
    public class EntityFinder: ObjectFinder
    {
        public void Start()
        {
            // register our json converters
            //JsonConverterContractResolver.Instance.RegisterJsonConverterForType();
        }

        public override (Dictionary<long, RecordedGameObjectState>, Dictionary<long, RecordedGameObjectState>) GetStateForCurrentFrame()
        {
            throw new System.NotImplementedException();
        }

        public override List<KeyFrameCriteria> GetKeyFrameCriteriaForCurrentFrame(out bool hasDeltas)
        {
            throw new System.NotImplementedException();
        }

        public override (Dictionary<long, ObjectStatus>, Dictionary<long, ObjectStatus>) GetObjectStatusForCurrentFrame()
        {
            throw new NotImplementedException();
        }

        public override Dictionary<long, PathBasedDeltaCount> ComputeNormalizedPathBasedDeltaCounts(Dictionary<long, ObjectStatus> priorStatusList, Dictionary<long, ObjectStatus> currentStatusList, out bool hasDelta)
        {
            throw new NotImplementedException();
        }

        public override void Cleanup()
        {
            throw new System.NotImplementedException();
        }
    }

}
