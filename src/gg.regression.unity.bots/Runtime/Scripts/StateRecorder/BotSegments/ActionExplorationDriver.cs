﻿using System;
using System.Collections.Generic;
using RegressionGames;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.KeyMoments.BotActions;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace StateRecorder.BotSegments
{
    public class ActionExplorationDriver : MonoBehaviour
    {

        // normally the first thing we do is retry the previous actions
        // these are sorted newest to oldest
        private readonly List<IBotActionData> PreviouslyCompletedActions = new(3);

        private readonly List<List<IBotActionData>> _previousActions = new(3);

        private int _previousActionIndex = 0;
        private int _previousActionSubIndex = 0;

        public bool IsExploring { get; private set;}

        public void ReportPreviouslyCompletedAction(IBotActionData action)
        {
            // limit to 3 prior actions
            while (PreviouslyCompletedActions.Count > 2)
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
            }
        }

        /**
         * Return true only when we start exploring, false if we were already exploring
         */
        public bool StartExploring()
        {
            if (!IsExploring)
            {
                _previousActions.Clear();
                _previousActionIndex = 0;
                _previousActionSubIndex = 0;
                List<IBotActionData> priorList = null;
                foreach (var previouslyCompletedAction in PreviouslyCompletedActions)
                {
                    // populate these lists so that we have a pattern of 0, 10, 210 where 0 is the newest and 2 is the oldest and we try them in patterns as listed
                    var actionList = new List <IBotActionData>(3);
                    actionList.Add(previouslyCompletedAction);
                    if (priorList != null)
                    {
                        actionList.AddRange(priorList);
                    }
                    priorList = actionList;
                    _previousActions.Add(actionList);
                }
                RGDebug.LogInfo("ActionExplorationDriver - Starting Exploratory Actions");
                IsExploring = true;
                return true;
            }

            return false;
        }

        private IBotActionData _inProgressAction = null;

        public void PerformExploratoryAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            error = null;
            if (!IsExploring)
            {
                return;
            }

            IBotActionData nextAction = _inProgressAction;

            if (nextAction == null)
            {
                if (_previousActionIndex >= _previousActions.Count)
                {
                    _previousActionIndex = 0;
                }

                // retry prior actions in pattern .. 0, 10, 210 where 2 is the oldest and 0 is the most recent
                if (_previousActionIndex < _previousActions.Count)
                {
                    var previousActions = _previousActions[_previousActionIndex];
                    if (_previousActionSubIndex >= previousActions.Count)
                    {
                        // move to the next list
                        _previousActionSubIndex = 0;
                        ++_previousActionIndex;
                        if (_previousActionIndex >= _previousActions.Count)
                        {
                            _previousActionIndex = 0;
                        }

                        previousActions = _previousActions[_previousActionIndex];
                    }

                    nextAction = previousActions[_previousActionSubIndex];
                    ++_previousActionSubIndex;
                    // process next action in the list
                }
            }

            if (nextAction != null)
            {
                var extraLog = "";
                if (nextAction is KeyMomentMouseActionData keyMomentMouseActionData)
                {
                    extraLog += " with first object path: " + keyMomentMouseActionData.mouseActions[1].clickedObjectNormalizedPaths[0];
                }
                RGDebug.LogInfo($"ActionExplorationDriver - Performing Exploratory Action of Type: {nextAction.GetType().Name}" + extraLog);
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

        public void StopExploring(int segmentNumber)
        {
            if (_inProgressAction != null)
            {
                _inProgressAction.AbortAction(segmentNumber);
                _inProgressAction = null;
            }
            if (IsExploring)
            {
                RGDebug.LogInfo("ActionExplorationDriver - Stopped Exploratory Actions");
            }
            IsExploring = false;
        }
    }
}