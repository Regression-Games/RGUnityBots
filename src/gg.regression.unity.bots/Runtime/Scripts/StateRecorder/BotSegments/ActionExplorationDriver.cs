using System;
using System.Collections.Generic;
using RegressionGames;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.KeyMoments.BotActions;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace StateRecorder.BotSegments
{

    public enum ExplorationState
    {
        STOPPED,
        PAUSED,
        EXPLORING
    }

    /**
     * This class controls action exploration during bot runs.  See BotSegmentsPlaybackController.ProcessBotSegmentAction for further information.
     */
    public class ActionExplorationDriver : MonoBehaviour
    {
        private const int PRIOR_ACTION_LIMIT = 5;

        // normally the first thing we do is retry the previous actions
        // these are sorted newest to oldest
        private readonly List<IBotActionData> PreviouslyCompletedActions = new(PRIOR_ACTION_LIMIT);

        private int _previousActionsNextIndex = 0;
        private int _previousActionsStartIndex = 0;

        public ExplorationState ExplorationState { get; private set; } = ExplorationState.STOPPED;

        public void ReportPreviouslyCompletedAction(IBotActionData action)
        {
            // limit to PRIOR_ACTION_LIMIT prior actions
            while (PreviouslyCompletedActions.Count >= PRIOR_ACTION_LIMIT)
            {
                // remove at front
                PreviouslyCompletedActions.RemoveAt(0);
            }

            if (!PreviouslyCompletedActions.Contains(action))
            {
                var extraLog = "";
                if (action is KeyMomentMouseActionData keyMomentMouseActionData)
                {
                    extraLog += " with first object path: " + keyMomentMouseActionData.mouseActions[1].clickedObjectNormalizedPaths[0];
                }
                RGDebug.LogInfo($"ActionExplorationDriver - Adding previously completed action of Type: {action.GetType().Name}" + extraLog);
                // insert at end
                PreviouslyCompletedActions.Add(action);
                _previousActionsNextIndex = PreviouslyCompletedActions.Count-1;
                _previousActionsStartIndex = PreviouslyCompletedActions.Count-1;
            }
        }

        public void StartExploring()
        {
            switch (ExplorationState)
            {
                case ExplorationState.STOPPED:
                    _previousActionsNextIndex = PreviouslyCompletedActions.Count-1;
                    _previousActionsStartIndex = PreviouslyCompletedActions.Count-1;
                    RGDebug.LogInfo("ActionExplorationDriver - Starting Exploratory Actions");
                    ExplorationState = ExplorationState.EXPLORING;
                    break;
                case ExplorationState.PAUSED:
                    RGDebug.LogInfo("ActionExplorationDriver - Resuming Exploratory Actions");
                    ExplorationState = ExplorationState.EXPLORING;
                    break;

            }

        }

        private IBotActionData _inProgressAction = null;

        /**
         * Chooses the next exploratory action to perform.
         *
         * The current implementation chooses one of the previous N successful IKeyMomentExploration actions to try on each update pass.
         */
        public void PerformExploratoryAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            error = null;
            if (ExplorationState.EXPLORING != ExplorationState)
            {
                return;
            }

            IBotActionData nextAction = _inProgressAction;

            if (nextAction == null)
            {
                nextAction = GetPriorActionToDo();
            }

            if (nextAction != null)
            {
                var extraLog = "";
                if (nextAction is KeyMomentMouseActionData keyMomentMouseActionData)
                {
                    extraLog += " with first object path: " + keyMomentMouseActionData.mouseActions[1].clickedObjectNormalizedPaths[0];
                }
                RGDebug.LogDebug($"ActionExplorationDriver - Performing Exploratory Action of Type: {nextAction.GetType().Name}" + extraLog);
                try
                {
                    // this handles actions that can take more than 1 update pass to process their action
                    // this could get weird for exploratory actions that have things like multi update waits for mouse holds or other things...
                    // but hopefully not.. hopefully if the real action interrupts us.. then it was truly ready to go
                    _inProgressAction = nextAction;
                    if (_inProgressAction.IsCompleted())
                    {
                        _inProgressAction.ReplayReset();
                        _inProgressAction.StartAction(segmentNumber, currentTransforms, currentEntities);
                    }

                    _inProgressAction.ProcessAction(segmentNumber, currentTransforms, currentEntities, out error);

                    if (_inProgressAction.IsCompleted())
                    {
                        _inProgressAction = null;
                    }
                    else if (error != null)
                    {
                        _inProgressAction.AbortAction(segmentNumber);
                        _inProgressAction = null;
                    }
                }
                catch (Exception)
                {
                    // no op
                }

            }
            else
            {
                // this is temporary until we have other exploration algorithms, but eventually this does have to give up...
                error = "No more available exploratory actions... Bot is stuck...";

                // TODO: Implement hooks to other exploration algorithms
            }

        }

        private IBotActionData GetPriorActionToDo()
        {
            // retry prior actions in pattern .. 0, 10, 210, etc... where 2 is the oldest and 0 is the most recent
            if (PreviouslyCompletedActions.Count > 0)
            {
                if (_previousActionsStartIndex < 0)
                {
                    // reset both
                    _previousActionsNextIndex = PreviouslyCompletedActions.Count - 1;
                    _previousActionsStartIndex = PreviouslyCompletedActions.Count - 1;
                }

                var nextAction = PreviouslyCompletedActions[_previousActionsNextIndex];

                if (--_previousActionsNextIndex < _previousActionsStartIndex)
                {
                    // update the startIndex and reset the next index
                    _previousActionsNextIndex = PreviouslyCompletedActions.Count - 1;
                    _previousActionsStartIndex--;
                }

                return nextAction;
            }

            _previousActionsNextIndex = PreviouslyCompletedActions.Count - 1;
            _previousActionsStartIndex = PreviouslyCompletedActions.Count - 1;
            return null;
        }

        public void PauseExploring(int segmentNumber)
        {
            if (_inProgressAction != null)
            {
                _inProgressAction.AbortAction(segmentNumber);
                _inProgressAction = null;
            }
            if (ExplorationState == ExplorationState.EXPLORING)
            {
                RGDebug.LogInfo("ActionExplorationDriver - Paused Exploratory Actions");
            }

            ExplorationState = ExplorationState.PAUSED;
        }

        public void StopExploring(int segmentNumber)
        {
            if (_inProgressAction != null)
            {
                _inProgressAction.AbortAction(segmentNumber);
                _inProgressAction = null;
            }
            if (ExplorationState == ExplorationState.EXPLORING)
            {
                RGDebug.LogInfo("ActionExplorationDriver - Stopped Exploratory Actions");
            }

            ExplorationState = ExplorationState.STOPPED;
        }
    }
}
