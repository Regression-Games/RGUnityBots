using System.Collections.Generic;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace StateRecorder.BotSegments
{
    public class ActionExplorationDriver : MonoBehaviour
    {

        // normally the first thing we do is retry the previous action
        private IBotActionData _previouslyCompletedAction = null;

        public bool IsExploring { get; private set;}

        private IBotActionData _activeAction = null;

        public void StartExploring(IBotActionData previousAction)
        {
            _previouslyCompletedAction = previousAction;
            IsExploring = true;
        }

        public void PerformExploratoryAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            error = null;
            if (!IsExploring)
            {
                return;
            }

            if (_previouslyCompletedAction != null)
            {
                _previouslyCompletedAction.ReplayReset();

                _activeAction = _previouslyCompletedAction;
                _previouslyCompletedAction = null;
            }

            if (_activeAction == null)
            {
                // TODO: Implement hooks to exploration algorithms here
            }

            if (_activeAction != null)
            {
                if (!_activeAction.IsCompleted())
                {
                    _activeAction.StartAction(segmentNumber, currentTransforms, currentEntities);
                    _activeAction.ProcessAction(segmentNumber, currentTransforms, currentEntities, out error);
                }
                else
                {
                    _activeAction = null;
                }
            }

        }

        public void StopExploring(int segmentNumber)
        {
            IsExploring = false;

            if (_activeAction != null)
            {
                _activeAction.AbortAction(segmentNumber);
                _activeAction = null;
            }
        }
    }
}
