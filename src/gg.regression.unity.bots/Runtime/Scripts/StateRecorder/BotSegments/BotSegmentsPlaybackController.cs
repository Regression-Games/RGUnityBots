using System;
using System.Collections.Generic;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
using RegressionGames.RGLegacyInputUtility;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ReSharper disable MergeIntoPattern

namespace RegressionGames.StateRecorder.BotSegments
{
    public class BotSegmentsPlaybackController : MonoBehaviour
    {
        public bool pauseEditorOnPlaybackWarning = false;

        private BotSegmentsPlaybackContainer _dataPlaybackContainer;

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
            KeyboardEventSender.Initialize();
            SceneManager.sceneLoaded += OnSceneLoad;
#if UNITY_EDITOR
            EditorApplication.pauseStateChanged += ResetErrorTimer;
#endif
        }

        private void OnDestroy()
        {
            MouseEventSender.Reset();
            SceneManager.sceneLoaded -= OnSceneLoad;
#if UNITY_EDITOR
            EditorApplication.pauseStateChanged -= ResetErrorTimer;
#endif
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
        }

        private bool unpaused;

#if UNITY_EDITOR
        private void ResetErrorTimer(PauseState pauseState)
        {
            if (pauseState == PauseState.Unpaused)
            {
                unpaused = true;
            }
            else
            {
                unpaused = false;
            }
        }
#endif

        private void OnDisable()
        {
            MouseEventSender.Reset();
        }

        public void SetDataContainer(BotSegmentsPlaybackContainer dataPlaybackContainer)
        {
            Stop();
            _replaySuccessful = null;

            MouseEventSender.Reset();

            _dataPlaybackContainer = dataPlaybackContainer;
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
            if (_dataPlaybackContainer != null)
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
            if (_dataPlaybackContainer != null)
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
            if (_nextBotSegments.Count > 0)
            {
                foreach (var nextBotSegment in _nextBotSegments)
                {
                    // stop any action
                    nextBotSegment.AbortAction();
                }
            }

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
            RGUtils.RestoreInputSettings();

            _dataPlaybackContainer = null;
            KeyboardEventSender.Reset();
            MouseEventSender.Reset();

            TransformStatus.Reset();
            KeyFrameEvaluator.Evaluator.Reset();
            var objectFinders = FindObjectsByType<ObjectFinder>(FindObjectsSortMode.None);
            foreach (var objectFinder in objectFinders)
            {
                objectFinder.Cleanup();
            }

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
            _dataPlaybackContainer?.Reset();
            KeyboardEventSender.Reset();
            MouseEventSender.Reset();

            TransformStatus.Reset();
            KeyFrameEvaluator.Evaluator.Reset();

            var objectFinders = FindObjectsByType<ObjectFinder>(FindObjectsSortMode.None);
            foreach (var objectFinder in objectFinders)
            {
                objectFinder.Cleanup();
            }
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
            _dataPlaybackContainer?.Reset();
            KeyboardEventSender.Reset();
            MouseEventSender.Reset();

            TransformStatus.Reset();
            KeyFrameEvaluator.Evaluator.Reset();

            var objectFinders = FindObjectsByType<ObjectFinder>(FindObjectsSortMode.None);
            foreach (var objectFinder in objectFinders)
            {
                objectFinder.Cleanup();
            }
        }

        public bool IsPlaying()
        {
            return _isPlaying;
        }

        public void Update()
        {
            if (_dataPlaybackContainer != null)
            {
                if (_startPlaying)
                {
                    // initialize the virtual mouse
                    MouseEventSender.InitializeVirtualMouse();

                    // start the mouse off the screen.. this avoids CV or other things failing because the virtual mouse is in the way at the start
                    MouseEventSender.MoveMouseOffScreen();

                    #if ENABLE_LEGACY_INPUT_MANAGER
                    RGLegacyInputWrapper.StartSimulation(this);
                    #endif
                    RGUtils.ConfigureInputSettings();
                    _startPlaying = false;
                    _isPlaying = true;
                    _nextBotSegments.Add(_dataPlaybackContainer.DequeueBotSegment());
                    // if starting to play, or on loop 1.. start recording
                    if (_loopCount < 2)
                    {
                        var gameFacePixelHashObserver = GameFacePixelHashObserver.GetInstance();
                        if (gameFacePixelHashObserver != null)
                        {
                            gameFacePixelHashObserver.SetActive(true);
                        }
                        _screenRecorder.StartRecording(_dataPlaybackContainer.SessionId);
                    }
                }
                if (_isPlaying)
                {
                    RGUtils.ForceApplicationFocus();
                }
            }
        }

        private float _lastTimeLoggedKeyFrameConditions = 0;

        private const int LOG_ERROR_INTERVAL = 10;

        private void LogPlaybackWarning(string loggedMessage)
        {
            var now = Time.unscaledTime;
            _lastTimeLoggedKeyFrameConditions = now;
            RGDebug.LogWarning(loggedMessage);
            FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText(loggedMessage);
            if (pauseEditorOnPlaybackWarning)
            {
                Debug.Break();
            }
        }

        private void EvaluateBotSegments()
        {
            var now = Time.unscaledTime;

            try
            {
                if (unpaused)
                {
                    _lastTimeLoggedKeyFrameConditions = now;
                    unpaused = false;
                }

                var objectFinders = Object.FindObjectsByType<ObjectFinder>(FindObjectsSortMode.None);

                var transformStatuses = new Dictionary<long, ObjectStatus>();
                var entityStatuses = new Dictionary<long, ObjectStatus>();

                foreach (var objectFinder in objectFinders)
                {
                    if (objectFinder is TransformObjectFinder)
                    {
                        transformStatuses = objectFinder.GetObjectStatusForCurrentFrame().Item2;
                    }
                    else
                    {
                        entityStatuses = objectFinder.GetObjectStatusForCurrentFrame().Item2;
                    }
                }

                // ProcessAction will occur up to 2 times in this method
                // once after checking if new inputs need to be processed, and one last time after checking if we need to get the next bot segment
                // the goal being to always play the inputs as quickly as possible
                BotSegment firstActionSegment = null;
                if (_nextBotSegments.Count > 0)
                {
                    firstActionSegment = _nextBotSegments[0];
                    try
                    {
                        var didAction = firstActionSegment.ProcessAction(transformStatuses, entityStatuses, out var error);
                        // only log this if we're really stuck on it
                        if (error == null && didAction)
                        {
                            // for every non error action, reset the timer
                            _lastTimeLoggedKeyFrameConditions = now;
                            FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText(null);
                        }

                        if (error != null && _lastTimeLoggedKeyFrameConditions < now - LOG_ERROR_INTERVAL)
                        {
                            var loggedMessage = $"({firstActionSegment.Replay_SegmentNumber}) - Bot Segment - Error processing BotAction\r\n" + error;
                            LogPlaybackWarning(loggedMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        var loggedMessage = $"({firstActionSegment.Replay_SegmentNumber}) - Bot Segment - Exception processing BotAction\r\n" + ex.Message;
                        LogPlaybackWarning(loggedMessage);
                        // uncaught exception... stop the segment
                        Stop();
                        throw;
                    }
                }

                var matchedThisUpdate = false;

                // check count each loop because we remove from it during the loop
                for (var i = 0; i < _nextBotSegments.Count; /* do not increment here*/)
                {
                    var nextBotSegment = _nextBotSegments[i];

                    var matched = nextBotSegment.Replay_Matched || nextBotSegment.endCriteria == null || nextBotSegment.endCriteria.Count == 0 || KeyFrameEvaluator.Evaluator.Matched(
                        i == 0,
                        nextBotSegment.Replay_SegmentNumber,
                        nextBotSegment.Replay_ActionCompleted,
                        nextBotSegment.endCriteria
                    );

                    if (matched)
                    {
                        // only update the time when the first index matches, but keeps us from logging this while waiting for actions to complete
                        if (i == 0)
                        {
                            if (!nextBotSegment.Replay_Matched)
                            {
                                // only mark this fully matched (as opposed to transient if it is the current segment)
                                nextBotSegment.Replay_Matched = true;
                                RGDebug.LogInfo($"({nextBotSegment.Replay_SegmentNumber}) - Bot Segment - Criteria Matched - {nextBotSegment.name} - {nextBotSegment.description}");

                                if (nextBotSegment.Replay_ActionStarted && !nextBotSegment.Replay_ActionCompleted)
                                {
                                    // tell the action that our segment completed and it should stop when it finishes its current actions.. only do this the first time we pass through as matched
                                    nextBotSegment.StopAction(transformStatuses, entityStatuses);
                                }

                                _lastTimeLoggedKeyFrameConditions = now;
                                FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText(null);
                                matchedThisUpdate = true;
                            }

                            // wait 10 seconds between logging this as some actions take quite a while
                            if (nextBotSegment.Replay_ActionStarted && !nextBotSegment.Replay_ActionCompleted && _lastTimeLoggedKeyFrameConditions < now - LOG_ERROR_INTERVAL)
                            {
                                _lastTimeLoggedKeyFrameConditions = now;
                                var loggedMessage = $"({nextBotSegment.Replay_SegmentNumber}) - Bot Segment - Waiting for actions to complete";
                                FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText(loggedMessage);
                                RGDebug.LogInfo(loggedMessage);
                            }
                        }

                        if (nextBotSegment.Replay_Matched && nextBotSegment.Replay_ActionStarted && nextBotSegment.Replay_ActionCompleted)
                        {
                            _lastTimeLoggedKeyFrameConditions = now;
                            RGDebug.LogInfo($"({nextBotSegment.Replay_SegmentNumber}) - Bot Segment - DONE - Criteria Matched && Action Completed - {nextBotSegment.name} - {nextBotSegment.description}");
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
                        // only log this every 10 seconds for the first key frame being evaluated after its actions complete
                        if (i == 0 && nextBotSegment.Replay_ActionCompleted && _lastTimeLoggedKeyFrameConditions < now - LOG_ERROR_INTERVAL)
                        {
                            var warningText = KeyFrameEvaluator.Evaluator.GetUnmatchedCriteria();
                            if (warningText != null)
                            {
                                var loggedMessage = $"({nextBotSegment.Replay_SegmentNumber}) - Bot Segment - Unmatched Criteria for \r\n" + warningText;
                                LogPlaybackWarning(loggedMessage);
                            }
                        }

                        ++i;
                    }
                }

                if (matchedThisUpdate)
                {
                    // only do this when a segment passed this update after all segments have been considered for this update
                    KeyFrameEvaluator.Evaluator.PersistPriorFrameStatus();
                }

                if (_nextBotSegments.Count > 0)
                {
                    // see if the last entry has transient matches.. if so.. dequeue another up to a limit of 2 total segments being evaluated... we may need to come back to this.. but without this look ahead, loading screens like bossroom fail due to background loading
                    // but if you go too far.. you can match segments in the replay that you won't see for another 50 segments when you go back to the menu again.. which is obviously wrong
                    var lastSegment = _nextBotSegments[^1];
                    if (lastSegment.Replay_TransientMatched)
                    {
                        if (_nextBotSegments.Count < 2)
                        {
                            var next = _dataPlaybackContainer.DequeueBotSegment();
                            if (next != null)
                            {
                                _lastTimeLoggedKeyFrameConditions = now;
                                FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText(null);
                                RGDebug.LogInfo($"({next.Replay_SegmentNumber}) - Bot Segment - Added {(next.HasTransientCriteria ? "" : "Non-")}Transient BotSegment for Evaluation after Transient BotSegment - {next.name} - {next.description}");
                                _nextBotSegments.Add(next);
                            }
                        }
                    }
                }
                else
                {
                    // segment list empty.. dequeue another
                    var next = _dataPlaybackContainer.DequeueBotSegment();
                    if (next != null)
                    {
                        _lastTimeLoggedKeyFrameConditions = now;
                        FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText(null);
                        RGDebug.LogInfo($"({next.Replay_SegmentNumber}) - Bot Segment - Added {(next.HasTransientCriteria ? "" : "Non-")}Transient BotSegment for Evaluation - {next.name} - {next.description}");
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
                        try
                        {
                            var didAction = nextSegment.ProcessAction(transformStatuses, entityStatuses, out var error);
                            // only log this if we're really stuck on it
                            if (error == null && didAction)
                            {
                                // for every non error action, reset the timer
                                _lastTimeLoggedKeyFrameConditions = now;
                                FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText(null);
                            }

                            // only log this if we're really stuck on it for a while
                            if (error != null && _lastTimeLoggedKeyFrameConditions < now - LOG_ERROR_INTERVAL)
                            {
                                var loggedMessage = $"({nextSegment.Replay_SegmentNumber}) - Bot Segment - Error processing BotAction\r\n" + error;
                                LogPlaybackWarning(loggedMessage);
                            }
                        }
                        catch (Exception ex)
                        {
                            var loggedMessage = $"({nextSegment.Replay_SegmentNumber}) - Bot Segment - Exception processing BotAction\r\n" + ex.Message;
                            LogPlaybackWarning(loggedMessage);
                            // uncaught exception... stop the segment
                            Stop();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var loggedMessage = $"(?) - Bot Segment - Exception processing BotSegments\r\n" + ex.Message;
                LogPlaybackWarning(loggedMessage);
                // uncaught exception... stop the segment
                Stop();
                throw;
            }
        }

        public void LateUpdate()
        {
            if (_isPlaying)
            {
                if (_dataPlaybackContainer != null)
                {
                    EvaluateBotSegments();
                }

                if (_nextBotSegments.Count == 0)
                {
                    MouseEventSender.MoveMouseOffScreen();

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

        public void OnGUI()
        {
            if (_isPlaying)
            {
                // render any GUI things for the first segment action
                if (_nextBotSegments.Count > 0)
                {
                    var transformStatuses = new Dictionary<long, ObjectStatus>();
                    var entityStatuses = new Dictionary<long, ObjectStatus>();
                    var objectFinders = Object.FindObjectsByType<ObjectFinder>(FindObjectsSortMode.None);

                    foreach (var objectFinder in objectFinders)
                    {
                        if (objectFinder is TransformObjectFinder)
                        {
                            transformStatuses = objectFinder.GetObjectStatusForCurrentFrame().Item2;
                        }
                        else
                        {
                            entityStatuses = objectFinder.GetObjectStatusForCurrentFrame().Item2;
                        }
                    }
                    _nextBotSegments[0].OnGUI(transformStatuses, entityStatuses);
                }
            }
        }
    }
}
