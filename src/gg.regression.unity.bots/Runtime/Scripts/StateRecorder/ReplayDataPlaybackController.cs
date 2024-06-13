﻿using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
#if ENABLE_LEGACY_INPUT_MANAGER
using RegressionGames.RGLegacyInputUtility;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
// ReSharper disable MergeIntoPattern

namespace RegressionGames.StateRecorder
{
    public partial class ReplayDataPlaybackController : MonoBehaviour
    {
        private ReplayBotSegmentsContainer _dataContainer;

        // flag to indicate the next update loop should start playing
        private bool _startPlaying;

        //tracks in playback is in progress or paused
        private bool _isPlaying;

        // 0 or greater == isLooping true
        private int _loopCount = -1;

        private Action<int> _loopCountCallback;

        // We track this as a list instead of a single entry to allow the UI and game object conditions to evaluate separately
        // We still only unlock the input sequences for a key frame once both UI and game object conditions are met
        // This is done this way to allow situations like when loading screens (UI) are changing while game objects are loading in the background and the process is not consistent/deterministic between the 2
        private readonly List<BotSegment> _nextBotSegments = new();

        // helps indicate if we made it through the full replay successfully
        private bool? _replaySuccessful;

        public string WaitingForKeyFrameConditions { get; private set; }

        private ScreenRecorder _screenRecorder;

        private void Start()
        {
            SetupEventSystem();
            SceneManager.sceneLoaded += OnSceneLoad;
        }

        private void OnDestroy()
        {
            MouseEventSender.Reset();
        }

        void OnSceneLoad(Scene s, LoadSceneMode m)
        {
            // since this is a don't destroy on load, we need to 'fix' the event systems in each new scene that loads
            SetupEventSystem();
        }

        private void SetupEventSystem()
        {
            RGUtils.SetupEventSystem();
        }

        void OnEnable()
        {
            _screenRecorder = GetComponentInParent<ScreenRecorder>();
            SetupEventSystem();
            MouseEventSender.GetMouse(true);
        }

        private void OnDisable()
        {
            MouseEventSender.Reset();
        }

        public void SetDataContainer(ReplayBotSegmentsContainer dataContainer)
        {
            Stop();
            _replaySuccessful = null;

            // get the mouse off the screen, when replay fails, we leave the virtual mouse cursor alone so they can see its location at time of failure, but on new file, we want this gone
            MouseEventSender.SendRawPositionMouseEvent(-1, new Vector2(Screen.width+20, -20));

            _dataContainer = dataContainer;
        }

        /**
         * <summary>Returns true only when the replay is complete and successful</summary>
         */
        public bool? ReplayCompletedSuccessfully()
        {
            return _replaySuccessful;
        }

        /**
         * <summary>Returns the current save location being used for the recording</summary>
         */
        public string SaveLocation()
        {
            return _screenRecorder.GetCurrentSaveDirectory();
        }

        public void Play()
        {
            if (_dataContainer != null)
            {
                if (!_startPlaying && !_isPlaying)
                {
                    _replaySuccessful = null;
                    _startPlaying = true;
                    _loopCount = -1;
                    _lastTimeLoggedKeyFrameConditions = Time.unscaledTime;
                }
            }
        }

        public void Loop(Action<int> loopCountCallback)
        {
            if (_dataContainer != null)
            {
                if (!_startPlaying && !_isPlaying)
                {
                    _replaySuccessful = null;
                    _startPlaying = true;
                    _loopCount = 1;
                    _loopCountCallback = loopCountCallback;
                    _loopCountCallback.Invoke(_loopCount);
                }
            }
        }

        public void Stop()
        {
            _nextBotSegments.Clear();
            _startPlaying = false;
            _isPlaying = false;
            _loopCount = -1;
            _replaySuccessful = null;
            WaitingForKeyFrameConditions = null;

            _screenRecorder.StopRecording();
            #if ENABLE_LEGACY_INPUT_MANAGER
            RGLegacyInputWrapper.StopSimulation();
            #endif

            _dataContainer = null;
            KeyboardEventSender.Reset();
            MouseEventSender.Reset();

            TransformStatus.Reset();
            KeyFrameEvaluator.Evaluator.Reset();

            InGameObjectFinder.GetInstance()?.Cleanup();
        }

        public void Reset()
        {
            _nextBotSegments.Clear();
            _startPlaying = false;
            _isPlaying = false;
            _loopCount = -1;
            _replaySuccessful = null;
            WaitingForKeyFrameConditions = null;

            _screenRecorder.StopRecording();
            #if ENABLE_LEGACY_INPUT_MANAGER
            RGLegacyInputWrapper.StopSimulation();
            #endif

            // similar to Stop, but assumes will play again
            _dataContainer?.Reset();
            KeyboardEventSender.Reset();
            MouseEventSender.Reset();

            TransformStatus.Reset();
            KeyFrameEvaluator.Evaluator.Reset();

            InGameObjectFinder.GetInstance()?.Cleanup();
        }

        public void ResetForLooping()
        {
            _nextBotSegments.Clear();
            _startPlaying = true;
            _isPlaying = false;
            // don't change _loopCount
            _replaySuccessful = null;
            WaitingForKeyFrameConditions = null;

            #if ENABLE_LEGACY_INPUT_MANAGER
            RGLegacyInputWrapper.StopSimulation();
            #endif

            // similar to Stop, but assumes continued looping .. doesn't stop recording
            _dataContainer?.Reset();
            KeyboardEventSender.Reset();
            MouseEventSender.Reset();

            TransformStatus.Reset();
            KeyFrameEvaluator.Evaluator.Reset();

            InGameObjectFinder.GetInstance()?.Cleanup();
        }

        public bool IsPlaying()
        {
            return _isPlaying;
        }

        public void Update()
        {
            if (_dataContainer != null)
            {
                if (_startPlaying)
                {
                    #if ENABLE_LEGACY_INPUT_MANAGER
                    RGLegacyInputWrapper.StartSimulation(this);
                    #endif
                    _startPlaying = false;
                    _isPlaying = true;
                    _nextBotSegments.Add(_dataContainer.DequeueBotSegment());
                    // if starting to play, or on loop 1.. start recording
                    if (_loopCount < 2)
                    {
                        _screenRecorder.StartRecording(_dataContainer.SessionId);
                    }
                }
            }
        }

        private float _lastTimeLoggedKeyFrameConditions = 0;

        private void EvaluateBotSegments()
        {
            var now = Time.unscaledTime;

            var currentUiTransforms = InGameObjectFinder.GetInstance().GetUITransformsForCurrentFrame().Item2;
            var currentGameObjectTransforms = InGameObjectFinder.GetInstance().GetGameObjectTransformsForCurrentFrame().Item2;

            // ProcessAction will occur up to 2 times in this method
            // once after checking if new inputs need to be processed, and one last time after checking if we need to get the next bot segment
            // the goal being to always play the inputs as quickly as possible
            BotSegment firstActionSegment = null;
            if (_nextBotSegments.Count > 0)
            {
                firstActionSegment = _nextBotSegments[0];
                firstActionSegment.ProcessAction(currentUiTransforms, currentGameObjectTransforms);
            }

            // check count each loop because we remove from it during the loop
            for (var i = 0; i < _nextBotSegments.Count; /* do not increment here*/)
            {
                var nextBotSegment = _nextBotSegments[i];

                var matched = nextBotSegment.Replay_Matched || KeyFrameEvaluator.Evaluator.Matched(nextBotSegment.keyFrameCriteria);

                if (matched)
                {
                    if (!nextBotSegment.Replay_Matched)
                    {
                        nextBotSegment.Replay_Matched = true;
                        // log this the first time
                        RGDebug.LogInfo($"({nextBotSegment.Replay_SegmentNumber}) - Bot Segment Criteria Matched");
                        if (i == 0)
                        {
                            _lastTimeLoggedKeyFrameConditions = now;
                            FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText(null);
                            // only do this when it is the zero index segment that passes
                            KeyFrameEvaluator.Evaluator.PersistPriorFrameStatus();
                        }
                    }

                    // only update the time when the first index matches, but keeps us from logging this while waiting for actions to complete
                    if (i == 0)
                    {
                        if (nextBotSegment.Replay_ActionStarted && !nextBotSegment.Replay_ActionCompleted && _lastTimeLoggedKeyFrameConditions < now - 5)
                        {
                            FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText($"({nextBotSegment.Replay_SegmentNumber}) - Waiting for actions to complete");
                        }
                    }

                    if (nextBotSegment.Replay_ActionStarted && nextBotSegment.Replay_ActionCompleted)
                    {
                        RGDebug.LogInfo($"({nextBotSegment.Replay_SegmentNumber}) - Bot Segment Completed");
                        //Process the inputs from that bot segment if necessary
                        _nextBotSegments.RemoveAt(i);
                    }
                    else
                    {
                        ++i;
                    }
                }
                else
                {
                    // only log this every 5 seconds for the first key frame being evaluated after its actions complete
                    if (i == 0 && nextBotSegment.Replay_ActionCompleted && _lastTimeLoggedKeyFrameConditions < now - 5)
                    {
                        var warningText = KeyFrameEvaluator.Evaluator.GetUnmatchedCriteria();
                        if (warningText != null)
                        {
                            var loggedMessage = $"({nextBotSegment.Replay_SegmentNumber}) - Unmatched Criteria for BotSegment\r\n" + warningText;
                            _lastTimeLoggedKeyFrameConditions = now;
                            RGDebug.LogInfo(loggedMessage);
                            FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText(loggedMessage);
                        }
                    }

                    ++i;
                }
            }

            if (_nextBotSegments.Count > 0)
            {
                // see if the last entry has transient matches.. if so.. dequeue another
                var lastSegment = _nextBotSegments[^1];
                if (lastSegment.Replay_TransientMatched)
                {
                    var next = _dataContainer.DequeueBotSegment();
                    if (next != null)
                    {
                        _lastTimeLoggedKeyFrameConditions = now;
                        FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText(null);
                        RGDebug.LogInfo($"({next.Replay_SegmentNumber}) - Added BotSegment for Evaluation after Transient BotSegment");
                        _nextBotSegments.Add(next);
                    }
                }
            }
            else
            {
                // segment list empty.. dequeue another
                var next = _dataContainer.DequeueBotSegment();
                if (next != null)
                {
                    _lastTimeLoggedKeyFrameConditions = now;
                    FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText(null);
                    RGDebug.LogInfo($"({next.Replay_SegmentNumber}) - Added BotSegment for Evaluation");
                    _nextBotSegments.Add(next);
                }
            }

            // if we moved to a new first segment in this frame, process its actions
            // if we didnt' move to a new frame, then the time hasn't changed within the same frame so no need to call this again
            if (_nextBotSegments.Count > 0)
            {
                var nextSegment = _nextBotSegments[0];
                if (nextSegment != firstActionSegment)
                {
                    var error = nextSegment.ProcessAction(currentUiTransforms, currentGameObjectTransforms);
                    if (error != null)
                    {
                        var loggedMessage = $"({nextSegment.Replay_SegmentNumber}) - Error processing BotAction\r\n" + error;
                        _lastTimeLoggedKeyFrameConditions = now;
                        RGDebug.LogInfo(loggedMessage);
                    }
                }
            }
        }

        public void LateUpdate()
        {
            if (_isPlaying)
            {
                if (_dataContainer != null)
                {
                    EvaluateBotSegments();
                }

                if (_nextBotSegments.Count == 0)
                {
                    MouseEventSender.SendRawPositionMouseEvent(-1, new Vector2(Screen.width+20, -20));

                    // we hit the end of the replay
                    if (_loopCount > -1)
                    {
                        ResetForLooping();
                        _loopCountCallback.Invoke(++_loopCount);
                    }
                    else
                    {
                        Reset();
                        _replaySuccessful = true;
                    }
                }
            }
        }
    }
}
