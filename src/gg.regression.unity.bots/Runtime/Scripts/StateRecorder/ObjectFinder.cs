using System.Collections.Generic;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder
{
    public abstract class ObjectFinder : MonoBehaviour
    {
        public abstract (Dictionary<long, RecordedGameObjectState>, Dictionary<long, RecordedGameObjectState>) GetStateForCurrentFrame();
        public abstract List<KeyFrameCriteria> GetKeyFrameCriteriaForCurrentFrame(out bool hasDeltas);
        public abstract (Dictionary<long, ObjectStatus>, Dictionary<long, ObjectStatus>) GetObjectStatusForCurrentFrame();
        public abstract Dictionary<long, PathBasedDeltaCount> ComputeNormalizedPathBasedDeltaCounts(Dictionary<long, ObjectStatus> priorStatusList, Dictionary<long, ObjectStatus> currentStatusList, out bool hasDelta);
        public abstract void Cleanup();
    }
}
