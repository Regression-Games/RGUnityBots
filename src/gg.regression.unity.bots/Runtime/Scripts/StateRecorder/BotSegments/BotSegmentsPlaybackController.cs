using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
using StateRecorder.BotSegments.Models;
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
    public enum PlayState
    {
        NotLoaded,
        Starting,
        Playing,
        Paused,
        Stopped
    }

    public class BotSegmentsPlaybackController : MonoBehaviour
    {
        public bool pauseEditorOnPlaybackWarning = false;

        private BotSegmentsPlaybackContainer _dataPlaybackContainer;

        //tracks in playback is in progress or paused or starting or stopped
        private PlayState _playState = PlayState.NotLoaded;

        // 0 or greater == isLooping true
        private int _loopCount = -1;

        private Action<int> _loopCountCallback;

        private string _lastSegmentPlaybackWarning = null;  

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
            KeyboardEventSender.Initialize();
            SceneManager.sceneLoaded += OnSceneLoad;
            SceneManager.sceneUnloaded += OnSceneUnload;
#if UNITY_EDITOR
            EditorApplication.pauseStateChanged += ResetErrorTimer;
#endif

            // if we have a checkpoint file, restart that sequence
            var checkpointFilePath = Application.temporaryCachePath + "/RegressionGames/SequenceRestart.json";
            if (File.Exists(checkpointFilePath))
            {
                using var sr = new StreamReader(File.OpenRead(checkpointFilePath));
                var checkpointDataString = sr.ReadToEnd();
                var checkpoint = JsonConvert.DeserializeObject<SequenceRestartCheckpoint>(checkpointDataString, JsonUtils.JsonSerializerSettings);
                if (checkpoint.apiVersion > SdkApiVersion.CURRENT_VERSION)
                {
                    RGDebug.LogWarning($"SequenceRestartCheckpoint file requires SDK version {checkpoint.apiVersion}, but the currently installed SDK version is {SdkApiVersion.CURRENT_VERSION}.  NOT resuming BotSequence from checkpoint data.");
                }
                else
                {
                    try
                    {
                        //TODO (REG-2170): Optionally support restarting from the next segment after restart instead of from the beginning...
                        var sequenceData = BotSequence.LoadSequenceJsonFromPath(checkpoint.resourcePath);
                        // remove the checkpoint so we don't resume from it again in the future
                        sr.Close();
                        File.Delete(checkpointFilePath);
                        RGDebug.LogInfo("Restarting a BotSequence from SequenceRestartCheckpoint file with resourcePath: " + checkpoint.resourcePath);
                        sequenceData.Item3.Play();
                    }
                    catch (Exception ex)
                    {
                        RGDebug.LogWarning($"Error loading or starting BotSequence specified in SequenceRestartCheckpoint file.  NOT resuming BotSequence from checkpoint data. - " + ex);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            MouseEventSender.Reset();
            SceneManager.sceneLoaded -= OnSceneLoad;
            SceneManager.sceneUnloaded -= OnSceneUnload;
#if UNITY_EDITOR
            EditorApplication.pauseStateChanged -= ResetErrorTimer;
#endif
        }

        void OnSceneLoad(Scene s, LoadSceneMode m)
        {
            if (_playState != PlayState.NotLoaded)
            {
                // since this is a don't destroy on load, we need to 'fix' the event systems in each new scene that loads
                RGUtils.SetupOverrideEventSystem(s);
            }
        }

        void OnSceneUnload(Scene s)
        {
            if (_playState != PlayState.NotLoaded)
            {
                RGUtils.TeardownOverrideEventSystem(s);
            }
        }

        void OnEnable()
        {
            _screenRecorder = GetComponentInParent<ScreenRecorder>();
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
            UnloadSegmentsAndReset();
            _replaySuccessful = null;
            _lastSegmentPlaybackWarning = null;

            MouseEventSender.Reset();

            _dataPlaybackContainer = dataPlaybackContainer;
            if (_dataPlaybackContainer != null)
            {
                _playState = PlayState.Stopped;
            }
            else
            {
                _playState = PlayState.NotLoaded;
            }
            LogHotkeyInformation();
        }

        public void LogHotkeyInformation()
        {
            RGDebug.LogInfo("BotSegments loaded.  Use the on screen overlay buttons or keyboard hotkeys to control playback.  CTRL+SHIFT_F9 = Play/Pause  , CTRL+SHIFT_F10 = Stop");
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

        public void Pause()
        {
            if (_dataPlaybackContainer != null)
            {
                if (_playState == PlayState.Playing)
                {
                    _playState = PlayState.Paused;

                    foreach (var nextBotSegment in _nextBotSegments)
                    {
                        nextBotSegment.PauseAction();
                        nextBotSegment.PauseValidations();
                    }
                }
            }
        }

        public void Play()
        {
            if (_dataPlaybackContainer != null)
            {
                if (_playState == PlayState.Stopped)
                {
                    _replaySuccessful = null;
                    _playState = PlayState.Starting;
                    _loopCount = -1;
                    _lastTimeLoggedKeyFrameConditions = Time.unscaledTime;
                }
                else if (_playState == PlayState.Paused)
                {
                    // resume
                    _playState = PlayState.Playing;

                    foreach (var nextBotSegment in _nextBotSegments)
                    {
                        nextBotSegment.UnPauseAction();
                        nextBotSegment.UnPauseValidations();
                    }
                }
            }
        }

        public void Loop(Action<int> loopCountCallback)
        {
            if (_dataPlaybackContainer != null)
            {
                if (_playState == PlayState.Stopped)
                {
                    _replaySuccessful = null;
                    _playState = PlayState.Starting;
                    _loopCount = 1;
                    _loopCountCallback = loopCountCallback;
                    _loopCountCallback.Invoke(_loopCount);
                }
            }
        }

        public void UnloadSegmentsAndReset()
        {
            if (_nextBotSegments.Count > 0)
            {
                foreach (var nextBotSegment in _nextBotSegments)
                {
                    // stop any action
                    nextBotSegment.AbortAction();
                    nextBotSegment.StopValidations();
                }
            }

            _nextBotSegments.Clear();
            _playState = PlayState.NotLoaded;
            BotSequence.ActiveBotSequence = null;
            _loopCount = -1;
            _replaySuccessful = null;
            WaitingForKeyFrameConditions = null;

            _screenRecorder.StopRecording();
            #if ENABLE_LEGACY_INPUT_MANAGER
            RGLegacyInputWrapper.StopSimulation();
            #endif
            RGUtils.TeardownOverrideEventSystem();
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

        public void Stop()
        {
            _nextBotSegments.Clear();
            _playState = PlayState.Stopped;
            _loopCount = -1;
            _replaySuccessful = null;
            WaitingForKeyFrameConditions = null;
            _lastSegmentPlaybackWarning = null;

            _screenRecorder.StopRecording();
            #if ENABLE_LEGACY_INPUT_MANAGER
            RGLegacyInputWrapper.StopSimulation();
            #endif
            RGUtils.TeardownOverrideEventSystem();
            RGUtils.RestoreInputSettings();

            // similar to UnloadSegmentsAndReset, but assumes will play again
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

        public void PrepareForNextLoop()
        {
            _nextBotSegments.Clear();
            _playState = PlayState.Starting;
            // don't change _loopCount
            _replaySuccessful = null;
            WaitingForKeyFrameConditions = null;
            _lastSegmentPlaybackWarning = null;

            #if ENABLE_LEGACY_INPUT_MANAGER
            RGLegacyInputWrapper.StopSimulation();
            #endif
            RGUtils.TeardownOverrideEventSystem();
            RGUtils.RestoreInputSettings();

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

        public PlayState GetState()
        {
            return _playState;
        }

        public string GetLastSegmentPlaybackWarning()
        {
            return _lastSegmentPlaybackWarning;
        }

        public void Update()
        {
            if (_dataPlaybackContainer != null)
            {
                if (_playState == PlayState.Starting)
                {
                    // initialize the virtual mouse
                    MouseEventSender.InitializeVirtualMouse();

                    // start the mouse off the screen.. this avoids CV or other things failing because the virtual mouse is in the way at the start
                    MouseEventSender.MoveMouseOffScreen();

                    RGUtils.SetupOverrideEventSystem();
                    #if ENABLE_LEGACY_INPUT_MANAGER
                    RGLegacyInputWrapper.StartSimulation(this);
                    #endif
                    RGUtils.ConfigureInputSettings();
                    _playState = PlayState.Playing;
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
                if (_playState == PlayState.Playing)
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
            _lastSegmentPlaybackWarning = loggedMessage;
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

                var objectFinders = FindObjectsByType<ObjectFinder>(FindObjectsSortMode.None);

                Dictionary<long, ObjectStatus> transformStatuses = null;
                Dictionary<long, ObjectStatus> entityStatuses = null;

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

                transformStatuses ??= new Dictionary<long, ObjectStatus>();
                entityStatuses ??= new Dictionary<long, ObjectStatus>();

                // track if any segment matched this update
                var matchedThisUpdate = false;

                // track if we have a new segment to evaluate... so long as we do, keep looping here before releasing from this Update call
                // thus we process each new segment as soon as possible and don't have any artificial one frame delays before processing
                var nextBotSegmentIndex = 0;
                while (nextBotSegmentIndex < _nextBotSegments.Count)
                {
                    // if we're working on the first entry in the list is the only time we do actions
                    if (nextBotSegmentIndex == 0)
                    {
                        BotSegment firstActionSegment = _nextBotSegments[0];
                        try
                        {
                            firstActionSegment.ProcessValidation();
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
                            // uncaught exception... stop and unload the segment
                            UnloadSegmentsAndReset();
                            throw;
                        }
                    }

                    var nextBotSegment = _nextBotSegments[nextBotSegmentIndex];

                    var matched = nextBotSegment.Replay_Matched || nextBotSegment.endCriteria == null || nextBotSegment.endCriteria.Count == 0 || KeyFrameEvaluator.Evaluator.Matched(
                        nextBotSegmentIndex == 0,
                        nextBotSegment.Replay_SegmentNumber,
                        nextBotSegment.Replay_ActionCompleted,
                        nextBotSegment.Replay_ValidationsCompleted,
                        nextBotSegment.endCriteria
                    );

                    if (matched)
                    {
                        // only update the time when the first index matches, but keeps us from logging this while waiting for actions to complete
                        if (nextBotSegmentIndex == 0)
                        {
                            if (!nextBotSegment.Replay_Matched)
                            {
                                // only mark this fully matched (as opposed to transient if it is the current segment)
                                nextBotSegment.Replay_Matched = true;
                                RGDebug.LogInfo($"({nextBotSegment.Replay_SegmentNumber}) - Bot Segment - Criteria Matched - {nextBotSegment.name ?? nextBotSegment.resourcePath} - {nextBotSegment.description}");

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
                            RGDebug.LogInfo($"({nextBotSegment.Replay_SegmentNumber}) - Bot Segment - DONE - Criteria Matched && Action Completed - {nextBotSegment.name ?? nextBotSegment.resourcePath} - {nextBotSegment.description}");
                            //Process the inputs from that bot segment if necessary
                            _nextBotSegments.RemoveAt(nextBotSegmentIndex);
                            // don't update the index since we shortened the list
                        }
                        else
                        {
                            ++nextBotSegmentIndex;
                        }
                    }
                    else
                    {
                        // only log this every 10 seconds for the first key frame being evaluated after its actions complete
                        if (nextBotSegmentIndex == 0 && nextBotSegment.Replay_ActionCompleted && _lastTimeLoggedKeyFrameConditions < now - LOG_ERROR_INTERVAL)
                        {
                            var warningText = KeyFrameEvaluator.Evaluator.GetUnmatchedCriteria();
                            if (warningText != null)
                            {
                                var loggedMessage = $"({nextBotSegment.Replay_SegmentNumber}) - Bot Segment - Unmatched Criteria for \r\n" + warningText;
                                LogPlaybackWarning(loggedMessage);
                            }
                        }
                        ++nextBotSegmentIndex;
                    }

                    // we possibly removed from the list above.. need this check
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
                                    RGDebug.LogInfo($"({next.Replay_SegmentNumber}) - Bot Segment - Added {(next.HasTransientCriteria ? "" : "Non-")}Transient BotSegment for Evaluation after Transient BotSegment - {next.name ?? next.resourcePath} - {next.description}");
                                    _nextBotSegments.Add(next);
                                    //next while loop iteration will get this guy
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
                            RGDebug.LogInfo($"({next.Replay_SegmentNumber}) - Bot Segment - Added {(next.HasTransientCriteria ? "" : "Non-")}Transient BotSegment for Evaluation - {next.name ?? next.resourcePath} - {next.description}");
                            _nextBotSegments.Add(next);
                            //next while loop iteration will get this guy
                        }
                    }
                }

                if (matchedThisUpdate)
                {
                    // only do this when a segment passed this update after all segments have been considered for this update
                    KeyFrameEvaluator.Evaluator.PersistPriorFrameStatus();
                }
            }
            catch (Exception ex)
            {
                var loggedMessage = $"(?) - Bot Segment - Exception processing BotSegments\r\n" + ex.Message;
                LogPlaybackWarning(loggedMessage);
                // uncaught exception... stop the segment
                UnloadSegmentsAndReset();
                throw;
            }
        }

        public void LateUpdate()
        {
            if (_playState == PlayState.Playing)
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
                        PrepareForNextLoop();
                        _loopCountCallback.Invoke(++_loopCount);
                    }
                    else
                    {
                        // stop ready to play again
                        Stop();
                        _replaySuccessful = true;
                    }
                }
            }
        }

        public void OnGUI()
        {
            if (_playState == PlayState.Playing || _playState == PlayState.Paused)
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
